using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Dearlife
{
    /// <summary>
    /// A person or a pet living in the house. It walks over the navigation surface (doors and gates open for it), sits on
    /// sofas and chairs, lies on beds and loungers, chats with the other person and pats the pets. Pets wander, sit,
    /// groom and sleep and stay on the ground floor, and do their own things too (Character.Pets.cs).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public partial class Character : MonoBehaviour
    {
        public static readonly List<Character> All = new List<Character>();

        static readonly List<Character> Everyone = new List<Character>();   // also the ones that are switched off

        /// <summary>Decorate mode stops everybody where they are and shows them as see-through silhouettes.</summary>
        public static bool Frozen { get; private set; }
        readonly List<(Renderer r, Material[] mats, UnityEngine.Rendering.ShadowCastingMode shadows)> original = new List<(Renderer, Material[], UnityEngine.Rendering.ShadowCastingMode)>();

        public static void SetAllFrozen(bool on, Material ghost)
        {
            Frozen = on;
            foreach (var c in Everyone.ToArray()) if (c) c.SetGhost(on, ghost);
        }

        void SetGhost(bool on, Material ghost)
        {
            if (on)
            {
                if (original.Count > 0 || !ghost) return;
                foreach (var r in GetComponentsInChildren<Renderer>())
                {
                    original.Add((r, r.sharedMaterials, r.shadowCastingMode));
                    var g = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < g.Length; i++) g[i] = ghost;
                    r.sharedMaterials = g;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            else
            {
                foreach (var o in original) if (o.r) { o.r.sharedMaterials = o.mats; o.r.shadowCastingMode = o.shadows; }
                original.Clear();
                if (agent && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
            }
        }

        void HoldStill()
        {
            if (agent && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
            rig.walkSpeed = 0f;
            if ((mode == Mode.Using || mode == Mode.Sitting) && spot != null)
            {
                SeatPose(out var p, out var q);   // still rides along if the piece is moved
                transform.SetPositionAndRotation(p, q);
            }
        }

        public string displayName = "";
        public bool isPet;
        public float scale = 1f;

        /// <summary>Something the player told a person to do (live mode). They are done one after the other before the person goes back to their own plans.</summary>
        public class Order
        {
            public enum Kind { Go, Use, Talk, Pet, Interact, Feed }
            public Interactable thing;
            public InteractionDef def;
            public System.Action after;   // done on arrival (Go) or once seated (Use), for switching the TV and the like
            public Kind kind;
            public Vector3 point;
            public UseSpot spot;
            public Character target;
            public string label;
        }

        public Sim sim;
        InteractionDef active; Interactable activeIt; float activeTime, payAccum, nextGrumble;
        bool feeding;
        public string ActiveLabel => active != null ? active.label : null;
        readonly Queue<Order> orders = new Queue<Order>();
        bool onOrder, holdSeat;
        Vector3 approachUsed;
        System.Action arrive, afterSeat;
        float autoResume;
        string doing = "";

        public int Queued => orders.Count;
        public bool OnOrder => onOrder;
        // someone doing what the player told them is not pulled into a chat on the way
        public bool CanChat => !onOrder && (mode == Mode.Idle || mode == Mode.Walk || mode == Mode.ChatWalk || mode == Mode.PetWalk);

        public string Activity
        {
            get
            {
                switch (mode)
                {
                    case Mode.Waiting: return "Just arrived";
                    case Mode.Interacting: return active != null ? active.label : "Busy";
                    case Mode.Walk: case Mode.ToSpot: return onOrder && !string.IsNullOrEmpty(doing) ? doing : "Walking";
                    case Mode.Sitting: case Mode.Using: return active != null ? active.label : spot != null && spot.pose == CharacterRig.Pose.Lie ? "Lying down" : "Sitting";
                    case Mode.Rising: return "Getting up";
                    case Mode.ChatWalk: return "Going to chat";
                    case Mode.Chatting: return "Chatting";
                    case Mode.PetWalk: return "Going to pet";
                    case Mode.Petting: return "Petting";
                    case Mode.BeingPetted: return "Being petted";
                    case Mode.PetActivity: return petDoing;
                    case Mode.Idle when isPet && rig:
                        switch (rig.pose)
                        {
                            case CharacterRig.Pose.Sleep: return "Sleeping";
                            case CharacterRig.Pose.Groom: return rig.kind == "dog" ? "Scratching an itch" : "Grooming";
                            case CharacterRig.Pose.Sit: return "Sitting";
                            default: return "Relaxing";
                        }
                    default: return "Relaxing";
                }
            }
        }

        public void GiveOrder(Order o)
        {
            orders.Enqueue(o);
            switch (mode)
            {
                case Mode.Interacting: FinishInteraction(false); SetIdle(0f); break;
                case Mode.Using: case Mode.Sitting: timer = 0f; break;          // stands up first, then does the new thing
                case Mode.Walk: case Mode.ChatWalk: case Mode.PetWalk:
                    if (!onOrder) { partner = null; SetIdle(0f); }
                    break;
                case Mode.ToSpot:
                    if (!onOrder) { if (spot) spot.occupant = null; spot = null; SetIdle(0f); }
                    break;
                case Mode.Chatting: partner = null; SetIdle(0f); break;
                case Mode.Petting: if (partner) partner.EndPetted(); partner = null; SetIdle(0f); break;
                case Mode.Idle: timer = 0f; break;
            }
        }

        /// <summary>Puts this person or pet somewhere else at once (a trip to another lot): whatever they were doing is dropped.</summary>
        public void TeleportTo(Vector3 p)
        {
            StopPetActivity();
            orders.Clear(); onOrder = false; arrive = null; afterSeat = null;
            if (active != null) FinishInteraction(false);
            if (spot) { spot.occupant = null; spot = null; }
            holdSeat = false; partner = null;
            if (agent)
            {
                agent.enabled = true;
                if (NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) p = hit.position;
                agent.Warp(p);
            }
            transform.position = p;
            if (rig) { rig.pose = CharacterRig.Pose.Stand; rig.walkSpeed = 0f; }
            SetIdle(0.6f);
        }

        public void CancelOrders()
        {
            orders.Clear();
            if (mode == Mode.Interacting) { FinishInteraction(false); SetIdle(0.3f); }
            else if (mode == Mode.Walk || mode == Mode.ChatWalk || mode == Mode.PetWalk) { partner = null; SetIdle(0.5f); }
            else if (mode == Mode.ToSpot) { if (spot) spot.occupant = null; spot = null; SetIdle(0.5f); }
            else if (mode == Mode.Using) timer = 0f;
            onOrder = false;
        }

        void FaceForward()
        {
            // after walking up to a thing, turn to face what is straight ahead of the walk
            var v = agent && agent.enabled ? agent.velocity : Vector3.zero; v.y = 0f;
            if (v.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(v);
        }

        void Say(string line, float seconds = 2.6f) { Bubble = line; bubbleUntil = Time.time + seconds; }

        void StartOrder()
        {
            var o = orders.Dequeue();
            onOrder = true;
            doing = o.label ?? "";
            bool ok = false;
            switch (o.kind)
            {
                case Order.Kind.Go:
                    ok = GoTo(o.point, 2.2f);
                    if (ok) { mode = Mode.Walk; arrive = o.after; }
                    break;
                case Order.Kind.Use:
                    if (o.spot != null && (o.spot.occupant == null) && GoToApproach(o.spot))
                    {
                        spot = o.spot; spot.occupant = this; mode = Mode.ToSpot; holdSeat = true; afterSeat = o.after; ok = true;
                    }
                    break;
                case Order.Kind.Talk:
                    if (o.target != null && o.target.CanChat && GoTo(o.target.transform.position, 1.8f)) { partner = o.target; mode = Mode.ChatWalk; ok = true; }
                    break;
                case Order.Kind.Pet:
                    if (o.target != null && GoTo(o.target.transform.position, 1.4f)) { partner = o.target; mode = Mode.PetWalk; ok = true; feeding = false; }
                    break;
                case Order.Kind.Feed:
                    if (o.target != null && Household.CanAfford(5) && GoTo(o.target.transform.position, 1.4f)) { partner = o.target; mode = Mode.PetWalk; ok = true; feeding = true; }
                    else if (o.target != null) Say("I need RM 5 for the pet food.");
                    break;
                case Order.Kind.Interact:
                    ok = StartInteraction(o.thing, o.def);
                    break;
            }
            if (!ok) { Say("Hmm, I can't get there."); SetIdle(0.05f); }
        }

        enum Mode { Waiting, Idle, Walk, ToSpot, Sitting, Using, Rising, ChatWalk, Chatting, PetWalk, Petting, BeingPetted, Interacting, PetActivity }

        NavMeshAgent agent;
        CharacterRig rig;
        Mode mode = Mode.Waiting;
        float timer, blend;
        UseSpot spot;
        Character partner;
        Vector3 fromPos, standPos;
        Quaternion fromRot;
        int lineIndex;
        float nextLine;

        public string Bubble { get; private set; }
        float bubbleUntil;
        public bool Speaking => Time.time < bubbleUntil;

        static readonly string[] Lines =
        {
            "Have you eaten yet?", "This house feels so cozy tonight.", "Shall I make some tea?", "I love the lights in the garden.",
            "Look how the pool glows.", "Did you feed the pets?", "Let's watch something later.", "Your day was good?",
            "The sofa is my favourite spot.", "I could stay here all evening.", "Thank you for today.", "Come, sit with me.",
        };

        void Awake()
        {
            Everyone.Add(this);
            agent = GetComponent<NavMeshAgent>();
            rig = GetComponent<CharacterRig>();
            agent.enabled = false;
            sim = GetComponent<Sim>();
            if (!sim) sim = gameObject.AddComponent<Sim>();
        }

        void OnDestroy() => Everyone.Remove(this);
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void Update()
        {
            UpdateVisibility();
            if (Frozen) { HoldStill(); return; }
            if (mode == Mode.Waiting)
            {
                if (DearlifeNav.Ready) Begin();
                return;
            }
            switch (mode)
            {
                case Mode.Idle: TickIdle(); break;
                case Mode.Walk: TickWalk(); break;
                case Mode.ToSpot: TickToSpot(); break;
                case Mode.Sitting: TickSitting(); break;
                case Mode.Using: TickUsing(); break;
                case Mode.Rising: TickRising(); break;
                case Mode.ChatWalk: TickChatWalk(); break;
                case Mode.Chatting: TickChatting(); break;
                case Mode.PetWalk: TickPetWalk(); break;
                case Mode.Petting: TickPetting(); break;
                case Mode.BeingPetted: TickBeingPetted(); break;
                case Mode.Interacting: TickInteracting(); break;
                case Mode.PetActivity: TickPetActivity(); break;
            }
            if (agent.enabled && agent.isOnNavMesh)
            {
                float v = agent.velocity.magnitude;
                if (mode == Mode.Walk || mode == Mode.ToSpot || mode == Mode.ChatWalk || mode == Mode.PetWalk)
                {
                    rig.pose = v > 0.15f ? CharacterRig.Pose.Walk : CharacterRig.Pose.Stand;
                    rig.walkSpeed = v;
                }
            }
        }

        void UpdateVisibility()
        {
            // somebody upstairs is not drawn while the upper floor is switched off
            var view = HouseView.Instance;
            bool show = !(transform.position.y > 2.8f && view && view.upperFloor && !view.upperFloor.activeSelf);
            foreach (var r in GetComponentsInChildren<Renderer>()) if (r.enabled != show) r.enabled = show;
        }

        // ---------- start and decisions ----------

        void Begin()
        {
            agent.agentTypeID = DearlifeNav.AgentType;
            agent.radius = isPet ? 0.2f : 0.24f;
            agent.height = 1.7f * scale;
            agent.speed = isPet ? (rig && rig.kind == "dog" ? 1.35f : 0.95f) : 1.1f;   // a relaxed walk (the walk clips stride at 0.8 to 1 m/s)
            agent.acceleration = 5f;
            agent.angularSpeed = 300f;
            agent.stoppingDistance = 0.1f;
            agent.autoBraking = true;
            if (isPet) agent.areaMask = ~(1 << DearlifeNav.StairsArea);   // pets stay on the ground floor
            if (NavMesh.SamplePosition(transform.position, out var hit, 3f, new NavMeshQueryFilter { agentTypeID = DearlifeNav.AgentType, areaMask = agent.areaMask }))
                transform.position = hit.position;
            agent.enabled = true;
            agent.Warp(transform.position);
            if (sim.displayName == "") sim.Setup(displayName, isPet);
            SetIdle(Random.Range(0.5f, 3f));
        }

        void SetIdle(float seconds)
        {
            if (mode == Mode.PetActivity) ReleasePet();
            mode = Mode.Idle;
            timer = seconds;
            rig.pose = CharacterRig.Pose.Stand;
            rig.walkSpeed = 0f;
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
        }

        void TickIdle()
        {
            if (orders.Count > 0) { StartOrder(); return; }
            if (onOrder) { onOrder = false; autoResume = Time.time + 12f; }   // told what to do: no wandering off for a while
            if (Time.time < autoResume) return;
            timer -= Time.deltaTime;
            if (timer <= 0f) Decide();
        }

        void Decide()
        {
            float r = Random.value;
            if (isPet)
            {
                if (PetDecide()) return;
                if (r < 0.45f && Wander(6f)) return;
                if (r < 0.65f) { Rest(CharacterRig.Pose.Sit, Random.Range(6f, 14f)); return; }
                if (r < 0.8f) { Rest(CharacterRig.Pose.Groom, Random.Range(5f, 9f)); return; }
                if (r < 0.92f) { Rest(CharacterRig.Pose.Sleep, Random.Range(20f, 45f)); return; }
                SetIdle(Random.Range(2f, 5f));
                return;
            }
            NoteFeelings();
            // the needs that are running low, the emptiest first: friends come from talking or petting, the rest from things in the house
            var order = new List<int> { 0, 1, 2, 3, 4, 5 };
            order.Sort((x, y) => sim.needs[x].CompareTo(sim.needs[y]));
            foreach (int ni in order)
            {
                if (sim.needs[ni] >= 38f) break;
                var need = (Need)ni;
                if (need == Need.Social) { if (TryChat() || TryPet()) return; }
                else if (TryFulfil(need)) return;
            }
            if (JobHours() && sim.job != "" && sim.Get(Need.Energy) > 35f && Random.value < 0.4f && TryWork()) return;
            if (r < 0.30f && TryFun()) return;
            if (r < 0.42f && TryFeedPet()) return;
            if (r < 0.40f && TryUseSpot()) return;
            if (r < 0.62f && Wander(40f)) return;
            if (r < 0.78f && TryChat()) return;
            if (r < 0.92f && TryPet()) return;
            SetIdle(Random.Range(2f, 6f));
        }

        void Rest(CharacterRig.Pose p, float seconds)
        {
            SetIdle(seconds);
            rig.pose = p;
            if (isPet && sim)
            {
                if (p == CharacterRig.Pose.Sleep) sim.Give(Need.Energy, seconds * 2f);
                else sim.Give(Need.Fun, seconds * 0.8f);
            }
        }

        bool Wander(float radius)
        {
            for (int tries = 0; tries < 10; tries++)
            {
                Vector3 p;
                if (radius > 20f)
                {
                    float x = Random.Range(-2.4f, 33f), z = Random.Range(-4.4f, 22f);
                    float y = (Random.value < 0.3f && x > 0.5f && x < 29.5f && z > 0.5f && z < 7.5f) ? 3.4f : 0.1f;
                    p = new Vector3(x, y, z);
                }
                else
                {
                    var d = Random.insideUnitCircle * radius;
                    p = transform.position + new Vector3(d.x, 0f, d.y);
                    p.x = Mathf.Clamp(p.x, -2.4f, 33f); p.z = Mathf.Clamp(p.z, -4.4f, 22f);
                }
                if (GoTo(p, 1.5f)) { mode = Mode.Walk; return true; }
            }
            return false;
        }

        /// <summary>The nearest navigation point at about the same height: the tops of tables and sofas are walkable islands too, and must not be picked.</summary>
        static bool SampleFloor(Vector3 p, float maxSnap, NavMeshQueryFilter filter, out NavMeshHit hit)
        {
            if (NavMesh.SamplePosition(p, out hit, maxSnap, filter) && Mathf.Abs(hit.position.y - p.y) < 0.3f) return true;
            for (float r = 0.25f; r <= maxSnap + 0.01f; r += 0.25f)
                for (int k = 0; k < 12; k++)
                {
                    float a = k * 30f * Mathf.Deg2Rad;
                    var q = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (NavMesh.SamplePosition(q, out hit, 0.2f, filter) && Mathf.Abs(hit.position.y - p.y) < 0.3f) return true;
                }
            hit = default;
            return false;
        }

        bool GoTo(Vector3 p, float maxSnap)
        {
            if (!agent.enabled || !agent.isOnNavMesh) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = DearlifeNav.AgentType, areaMask = agent.areaMask };
            if (!SampleFloor(p, maxSnap, filter, out var hit)) return false;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            agent.SetPath(path);
            return true;
        }

        bool Arrived() => agent.enabled && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.12f;

        void TickWalk()
        {
            if (Arrived())
            {
                var a = arrive; arrive = null;
                if (a != null) { FaceForward(); a(); }
                if (mode == Mode.Walk) SetIdle(onOrder ? 0.05f : Random.Range(1.5f, 5f));
            }
        }

        // ---------- furniture ----------

        /// <summary>
        /// Walks to the usual spot in front of a seat or bed; when that is blocked (a coffee table close to the sofa) the person
        /// comes from the side or the front instead, and gets up again at the same place.
        /// </summary>
        bool GoToApproach(UseSpot s)
        {
            if (GoTo(s.ApproachWorld, 1.2f)) { approachUsed = s.ApproachWorld; return true; }
            var facing = Quaternion.Euler(0f, (s.transform.parent ? s.transform.parent.eulerAngles.y : 0f) + s.yaw, 0f);
            var filter = new NavMeshQueryFilter { agentTypeID = DearlifeNav.AgentType, areaMask = agent.areaMask };
            foreach (float r in new[] { 1.1f, 1.5f, 1.9f, 2.4f })
                for (int k = 0; k < 8; k++)
                {
                    float a = k * 45f;                                                // straight in front first, then round the piece
                    var p = s.transform.position + facing * (Quaternion.Euler(0f, a, 0f) * Vector3.forward * r);
                    p.y = transform.position.y;
                    if (!SampleFloor(p, 0.5f, filter, out var hit)) continue;
                    if (GoTo(hit.position, 0.3f)) { approachUsed = hit.position; return true; }
                }
            return false;
        }

        bool TryUseSpot()
        {
            var free = new List<UseSpot>();
            foreach (var s in UseSpot.All) if (s.occupant == null && s.gameObject.activeInHierarchy && s.label != "bath") free.Add(s);   // nobody lies in an empty bath
            for (int tries = 0; tries < 6 && free.Count > 0; tries++)
            {
                var s = free[Random.Range(0, free.Count)];
                if (!GoToApproach(s)) { free.Remove(s); continue; }
                spot = s; s.occupant = this; mode = Mode.ToSpot;
                return true;
            }
            return false;
        }

        void TickToSpot()
        {
            if (spot == null) { SetIdle(1f); return; }
            if (!Arrived()) return;
            agent.enabled = false;
            fromPos = transform.position; fromRot = transform.rotation;
            blend = 0f; mode = Mode.Sitting;
            rig.seat = spot;
            rig.pose = spot.pose == CharacterRig.Pose.Lie ? CharacterRig.Pose.Lie : CharacterRig.Pose.Sit;
            rig.walkSpeed = 0f;
        }

        void SeatPose(out Vector3 pos, out Quaternion rot)
        {
            // sitting: the pelvis is lowered by the pose, so the root goes where the pelvis target minus the hip height is
            rig.pose = spot.pose;
            rig.seat = spot;
            spot.RootFor(rig, scale, out pos, out rot);
        }

        void TickSitting()
        {
            blend += Time.deltaTime / 0.8f;
            SeatPose(out var pos, out var rot);
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blend));
            transform.SetPositionAndRotation(Vector3.Lerp(fromPos, pos, e), Quaternion.Slerp(fromRot, rot, e));
            if (blend >= 1f)
            {
                mode = Mode.Using; timer = holdSeat ? 150f : Random.Range(spot.seconds.x, spot.seconds.y);
                bool ordered = holdSeat; holdSeat = false;
                var seated = afterSeat; afterSeat = null;
                if (seated != null) seated();
                else if (!ordered)
                {
                    var tv = TvScreen.Facing(spot);       // sat down in front of the TV on their own: sometimes puts it on
                    if (tv != null && !tv.on && Random.value < 0.6f) tv.SetOn(true, true);
                }
            }
        }

        void TickUsing()
        {
            SeatPose(out var pos, out var rot);
            transform.SetPositionAndRotation(pos, rot);   // follows the piece if it is moved
            timer -= Time.deltaTime;
            if (active != null) TickActive(Time.deltaTime);
            if (orders.Count > 0) timer = 0f;   // told to do something else
            if (timer <= 0f)
            {
                FinishInteraction(true);
                var tvLeft = TvScreen.Facing(spot);
                if (tvLeft != null && tvLeft.on && tvLeft.autoSwitched && !TvScreen.AnyoneWatching(tvLeft, this)) tvLeft.SetOn(false);
                fromPos = transform.position; fromRot = transform.rotation;
                standPos = approachUsed;
                blend = 0f; mode = Mode.Rising;
                rig.pose = CharacterRig.Pose.Stand;
            }
        }

        void TickRising()
        {
            blend += Time.deltaTime / 0.7f;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(blend));
            var upright = Quaternion.Euler(0f, fromRot.eulerAngles.y, 0f);
            var faceOut = Quaternion.LookRotation(new Vector3(standPos.x - fromPos.x, 0f, standPos.z - fromPos.z).sqrMagnitude > 0.01f ? new Vector3(standPos.x - fromPos.x, 0f, standPos.z - fromPos.z) : transform.forward);
            transform.SetPositionAndRotation(Vector3.Lerp(fromPos, standPos, e), Quaternion.Slerp(fromRot, faceOut, e));
            if (blend >= 1f)
            {
                if (spot) spot.occupant = null;
                spot = null;
                agent.enabled = true;
                agent.Warp(standPos);
                SetIdle(Random.Range(1f, 3f));
            }
        }

        // ---------- doing things with objects ----------

        /// <summary>Walks to the object (or its seat) and starts doing what it offers. False if there is no way to get there or no money for it.</summary>
        public bool StartInteraction(Interactable it, InteractionDef d)
        {
            if (it == null || d == null || !it.gameObject.activeInHierarchy) return false;
            if (d.cost > 0 && !Household.CanAfford(d.cost)) { Say("I can't afford that right now."); return false; }
            if (d.seat)
            {
                UseSpot best = null; float bestD = float.MaxValue;
                foreach (var sp in it.GetComponentsInChildren<UseSpot>())
                {
                    if (sp.occupant != null && sp.occupant != this) continue;
                    if (d.needsTv && TvScreen.Facing(sp) == null) continue;
                    if ((d.pose == CharacterRig.Pose.Lie) != (sp.pose == CharacterRig.Pose.Lie)) continue;
                    float dd = (sp.transform.position - transform.position).sqrMagnitude;
                    if (dd < bestD) { bestD = dd; best = sp; }
                }
                if (best == null || !GoToApproach(best)) return false;
                spot = best; spot.occupant = this; mode = Mode.ToSpot; holdSeat = true;
                afterSeat = () => BeginInteraction(it, d);
                return true;
            }
            if (d.sitNear && SitNear(it, d)) return true;
            bool going = false;
            foreach (var p in it.StandPoints(transform.position))
                if (GoTo(p, 0.8f)) { going = true; break; }
            if (!going) return false;
            mode = Mode.Walk;
            arrive = () => BeginInteraction(it, d);
            return true;
        }

        /// <summary>
        /// A meal at the table or work at a desk: a free chair beside it, facing it (an office chair is turned round to
        /// face the desk, it swivels). False when there is none, and then it is done standing.
        /// </summary>
        bool SitNear(Interactable it, InteractionDef d)
        {
            var f = it.GetComponent<Furniture>();
            var lb = f ? f.LocalBounds : new Bounds(Vector3.zero, Vector3.one);
            UseSpot best = null; float bestD = float.MaxValue;
            foreach (var sp in UseSpot.All)
            {
                if (sp == null || !sp.isActiveAndEnabled || sp.pose != CharacterRig.Pose.Sit || (sp.occupant != null && sp.occupant != this)) continue;
                if (sp.label != "dining chair" && sp.label != "office chair" && sp.label != "bar stool") continue;
                var local = it.transform.InverseTransformPoint(sp.transform.position);
                // how far outside the table or desk the seat is (0 when over it)
                float dx = Mathf.Max(0f, Mathf.Abs(local.x - lb.center.x) - lb.extents.x), dz = Mathf.Max(0f, Mathf.Abs(local.z - lb.center.z) - lb.extents.z);
                if (Mathf.Abs(sp.transform.position.y - it.transform.position.y) > 1.2f || Mathf.Sqrt(dx * dx + dz * dz) > 0.75f) continue;
                bool swivel = sp.label == "office chair";
                var toItem = it.Centre - sp.transform.position; toItem.y = 0f;
                if (!swivel && Vector3.Dot(SpotForward(sp), toItem.normalized) < 0.3f) continue;   // a chair facing away is not used
                float dd = (sp.transform.position - transform.position).sqrMagnitude;
                if (dd < bestD) { bestD = dd; best = sp; }
            }
            if (best == null) return false;
            if (best.label == "office chair" && best.transform.parent)
            {
                // turn the chair round to face the desk
                var chair = best.transform.parent;
                var toDesk = it.Centre - best.transform.position; toDesk.y = 0f;
                if (toDesk.sqrMagnitude > 0.01f)
                {
                    float want = Quaternion.LookRotation(toDesk).eulerAngles.y - best.yaw;
                    var pivot = best.transform.position;
                    chair.rotation = Quaternion.Euler(0f, want, 0f);
                    chair.position += pivot - best.transform.position;
                }
            }
            if (!GoToApproach(best)) return false;
            spot = best; spot.occupant = this; mode = Mode.ToSpot; holdSeat = true;
            afterSeat = () => BeginInteraction(it, d);
            return true;
        }

        static Vector3 SpotForward(UseSpot s) => Quaternion.Euler(0f, (s.transform.parent ? s.transform.parent.eulerAngles.y : 0f) + s.yaw, 0f) * Vector3.forward;

        AudioSource actSound;
        bool onBelt;             // walking on the treadmill
        GameObject plate;        // the meal on the table in front of someone eating there

        /// <summary>A plate of food (or a takeaway box) on the table in front of the chair.</summary>
        void TablePlate(Interactable it, InteractionDef d)
        {
            if (d.id != "meal" && d.id != "takeaway") return;
            var fwd = SpotForward(spot);
            var at = spot.transform.position + fwd * 0.42f;
            // the table top under that point
            float top = it.transform.position.y + 0.76f;
            foreach (var h in Physics.RaycastAll(at + Vector3.up * 1.5f, Vector3.down, 2.5f))
                if (h.transform.IsChildOf(it.transform)) { top = Mathf.Max(top - 1f, h.point.y); break; }
            plate = new GameObject(d.id == "meal" ? "Plate" : "Takeaway box");
            plate.transform.position = new Vector3(at.x, top, at.z);
            plate.transform.rotation = Quaternion.LookRotation(-fwd);
            if (d.id == "meal")
            {
                Prop(plate.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.006f, 0f), new Vector3(0.26f, 0.006f, 0.26f), new Color(0.95f, 0.95f, 0.93f), 0.8f);
                Prop(plate.transform, PrimitiveType.Sphere, new Vector3(-0.03f, 0.03f, 0.01f), new Vector3(0.12f, 0.05f, 0.1f), new Color(0.93f, 0.9f, 0.8f), 0.3f);   // rice
                Prop(plate.transform, PrimitiveType.Sphere, new Vector3(0.05f, 0.028f, -0.02f), new Vector3(0.08f, 0.045f, 0.07f), new Color(0.62f, 0.3f, 0.12f), 0.5f);   // curry
                Prop(plate.transform, PrimitiveType.Cube, new Vector3(0.15f, 0.004f, 0f), new Vector3(0.012f, 0.004f, 0.18f), new Color(0.75f, 0.76f, 0.78f), 0.8f);   // spoon
            }
            else
            {
                Prop(plate.transform, PrimitiveType.Cube, new Vector3(0f, 0.035f, 0f), new Vector3(0.2f, 0.07f, 0.14f), new Color(0.72f, 0.55f, 0.36f), 0.2f);
                Prop(plate.transform, PrimitiveType.Cube, new Vector3(0f, 0.071f, 0f), new Vector3(0.21f, 0.004f, 0.15f), new Color(0.6f, 0.2f, 0.15f), 0.3f);
            }
        }

        static readonly Dictionary<Color, Material> propMats = new Dictionary<Color, Material>();

        static void Prop(Transform parent, PrimitiveType type, Vector3 pos, Vector3 size, Color c, float smooth)
        {
            var g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = size;
            if (!propMats.TryGetValue(c, out var m) || !m)
            {
                m = new Material(Shader.Find("HDRP/Lit")) { name = "Table prop" };
                m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth);
                propMats[c] = m;
            }
            g.GetComponent<Renderer>().sharedMaterial = m;
        }
        readonly List<Transform> liftedGuitar = new List<Transform>();

        /// <summary>A book for reading, a mug for coffee, weights for working out, and the guitar itself lifted off its stand.</summary>
        void HoldForInteraction(Interactable it, InteractionDef d)
        {
            if (rig == null || isPet) return;
            switch (d.id)
            {
                case "read": rig.Hold("book"); break;
                case "coffee": rig.Hold("mug"); break;
                case "workout": rig.Hold("dumbbell"); break;
                case "guitar":
                {
                    // the parts of the guitar (named G_...) come off the stand as a copy that goes into the hands
                    if (it == null) break;
                    var parts = new List<Transform>();
                    foreach (Transform c in it.transform) if (c.name.StartsWith("G_")) parts.Add(c);
                    if (parts.Count == 0) break;
                    var holder = new GameObject("Guitar in hands");
                    holder.transform.SetParent(it.transform, false);
                    foreach (var p in parts)
                    {
                        var cl = Instantiate(p.gameObject, holder.transform);
                        cl.transform.localPosition = p.localPosition; cl.transform.localRotation = p.localRotation; cl.transform.localScale = p.localScale;
                        p.gameObject.SetActive(false);
                        liftedGuitar.Add(p);
                    }
                    rig.Hold("guitar", holder);
                    break;
                }
            }
        }

        void ReleaseHeld()
        {
            if (rig) rig.Release();
            foreach (var p in liftedGuitar) if (p) p.gameObject.SetActive(true);
            liftedGuitar.Clear();
        }

        void BeginInteraction(Interactable it, InteractionDef d)
        {
            if (d.cost > 0 && !Household.Spend(d.cost, d.label.ToLower())) { Say("I can't afford that right now."); if (spot != null) timer = 0f; return; }
            active = d; activeIt = it; activeTime = 0f; payAccum = 0f;
            it.user = this;
            GameAudio.StopAct(actSound);
            actSound = GameAudio.PlayAct(d.id, d.seconds, it.Centre);
            if (d.id == "bath" && it.GetComponent<BathTub>() is BathTub tub && tub) tub.Fill(true);
            if (d.needsTv && spot != null) { var tv = TvScreen.Facing(spot); if (tv != null && !tv.on) tv.SetOn(true, true); }
            if (d.sitNear && spot != null && mode == Mode.Using) TablePlate(it, d);
            if (d.seat || (d.sitNear && spot != null && mode == Mode.Using)) { timer = d.seconds; return; }   // seated: TickUsing runs it
            mode = Mode.Interacting; timer = d.seconds; blend = 0f;
            fromPos = transform.position; fromRot = transform.rotation;
            rig.pose = d.pose; rig.walkSpeed = 0f;
            HoldForInteraction(it, d);
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            if (it && it.id == "treadmill")
            {
                // on the belt, walking without getting anywhere
                standPos = transform.position; agent.enabled = false;
                transform.SetPositionAndRotation(it.transform.TransformPoint(new Vector3(0f, 0.17f, -0.1f)), it.transform.rotation);
                rig.Release();
                rig.pose = CharacterRig.Pose.Walk; rig.inPlaceSpeed = 1.3f;
                onBelt = true;
            }
            if (d.pose == CharacterRig.Pose.Swim)
            {
                standPos = transform.position; agent.enabled = false; GameAudio.Play(GameAudio.Sfx.Splash);
            }
            if (d.id == "shower" && it && it.GetComponent<ShowerStall>() is ShowerStall st && st)
            {
                shower = st; showerStep = 0; showerT = 0f; showerRunning = false;
                standPos = transform.position; agent.enabled = false;
            }
        }

        // ---- the shower: open the sliding door, step in, shut it, wash in the steam, and back out the same way

        ShowerStall shower;
        int showerStep;
        float showerT;
        bool showerRunning;

        static Vector3 Along(Vector3 a, Vector3 b, Vector3 c, float f)
        {
            float ab = Vector3.Distance(a, b), bc = Vector3.Distance(b, c), d = Mathf.Clamp01(f) * (ab + bc);
            return d <= ab ? Vector3.Lerp(a, b, ab > 0f ? d / ab : 1f) : Vector3.Lerp(b, c, bc > 0f ? (d - ab) / bc : 1f);
        }

        void TickShower()
        {
            var st = shower;
            showerT += Time.deltaTime;
            Vector3 outside = standPos, inside = st.Inside, door = st.Doorway;
            door.y = Mathf.Lerp(outside.y, inside.y, 0.5f);
            Vector3 was = transform.position;
            switch (showerStep)
            {
                case 0:                                                        // the door slides open
                    st.open = true;
                    rig.pose = CharacterRig.Pose.Stand;
                    Face(-st.OutwardDir, 8f);
                    if (showerT > 0.8f) { showerStep = 1; showerT = 0f; }
                    break;
                case 1:                                                        // step in
                {
                    float f = showerT / 1.8f;
                    transform.position = Along(outside, door, inside, f);
                    rig.pose = CharacterRig.Pose.Walk; rig.walkSpeed = 0.9f;
                    Face(transform.position - was, 10f);
                    if (f >= 1f) { showerStep = 2; showerT = 0f; st.open = false; }
                    break;
                }
                case 2:                                                        // shut in, water on
                    rig.pose = CharacterRig.Pose.Wash; rig.walkSpeed = 0f;
                    transform.position = inside;
                    Face(st.OutwardDir, 2.5f);
                    if (!showerRunning && showerT > 0.7f) { st.Running(true, transform); showerRunning = true; }
                    if (timer <= 3.6f) { st.Running(false, transform); showerRunning = false; st.open = true; showerStep = 3; showerT = 0f; }
                    break;
                case 3:                                                        // water off, door open, step out
                {
                    rig.pose = showerT < 0.7f ? CharacterRig.Pose.Stand : CharacterRig.Pose.Walk;
                    rig.walkSpeed = 0.9f;
                    float f = (showerT - 0.7f) / 1.8f;
                    if (f > 0f) { transform.position = Along(inside, door, outside, f); Face(transform.position - was, 10f); }
                    else Face(st.OutwardDir, 8f);
                    if (f >= 1f) { st.open = false; showerStep = 4; }
                    break;
                }
                default:
                    rig.pose = CharacterRig.Pose.Stand; rig.walkSpeed = 0f;
                    break;
            }
        }

        void Face(Vector3 dir, float speed)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * speed);
        }

        void EndShower()
        {
            if (!shower) return;
            shower.Running(false, transform);
            shower.open = false;
            // (interrupted inside: back in front of the door)
            transform.position = standPos;
            agent.enabled = true;
            agent.Warp(standPos);
            shower = null; showerRunning = false;
        }

        void TickActive(float dt)
        {
            activeTime += dt;
            foreach (var f in active.fills) sim.Give(f.need, f.perSecond * dt);
            if (active.job)
            {
                float mult = 1f; float.TryParse(active.category, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out mult);
                payAccum += Household.PayPerSecond(sim) * (mult <= 0f ? 1f : mult) * dt;
                sim.WorkXp(dt);
            }
        }

        void TickInteracting()
        {
            if (active == null) { SetIdle(0.2f); return; }
            TickActive(Time.deltaTime);
            timer -= Time.deltaTime;
            var centre = activeIt ? activeIt.Centre : transform.position + transform.forward;
            if (shower) TickShower();
            else if (active.pose == CharacterRig.Pose.Swim)
            {
                blend = Mathf.Min(1f, blend + Time.deltaTime / 1.2f);
                float t = activeTime;
                var c = centre;
                var half = activeIt && activeIt.poolHalf.x > 0f ? activeIt.poolHalf : new Vector2(4.1f, 2f);
                var pos = new Vector3(c.x + Mathf.Sin(t * 0.45f) * half.x * 0.75f, c.y - 0.1f, c.z + Mathf.Sin(t * 0.9f) * half.y * 0.45f);
                var dir = new Vector3(Mathf.Cos(t * 0.45f) * Mathf.Sign(Mathf.Cos(t * 0.45f)), 0f, 0.1f);
                float yaw = Mathf.Cos(t * 0.45f) >= 0f ? 90f : -90f;
                var rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
                var root = pos - rot * new Vector3(0f, rig.RestHip * scale, 0f);
                float e = Mathf.SmoothStep(0f, 1f, blend);
                transform.SetPositionAndRotation(Vector3.Lerp(fromPos, root, e), Quaternion.Slerp(fromRot, rot, e));
            }
            else if (!onBelt)
            {
                var d = centre - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 6f);
            }
            if (timer <= 0f)
            {
                bool swim = active.pose == CharacterRig.Pose.Swim || onBelt;
                var stand = activeIt && activeIt.hasCustomStand ? activeIt.customStand : onBelt ? standPos : transform.position;
                FinishInteraction(true);
                if (swim)
                {
                    fromPos = transform.position; fromRot = transform.rotation; standPos = stand; blend = 0f; mode = Mode.Rising; rig.pose = CharacterRig.Pose.Stand;
                }
                else SetIdle(Random.Range(0.4f, 1.5f));
            }
        }

        void FinishInteraction(bool completed)
        {
            if (active == null) return;
            var d = active; active = null;
            GameAudio.StopAct(actSound); actSound = null;
            ReleaseHeld();
            if (d.id == "bath" && activeIt && activeIt.GetComponent<BathTub>() is BathTub tub && tub) tub.Fill(false);
            if (plate) { Destroy(plate); plate = null; }
            if (onBelt)
            {
                onBelt = false; rig.inPlaceSpeed = 0f;
                if (mode != Mode.Rising) { transform.position = standPos; agent.enabled = true; agent.Warp(standPos); }
            }
            EndShower();
            if (activeIt) activeIt.user = null;
            activeIt = null;
            bool worth = completed || activeTime > d.seconds * 0.5f;
            if (worth && sim)
            {
                if (d.skill.HasValue) sim.AddXp(d.skill.Value, d.xp * Mathf.Clamp01(activeTime / Mathf.Max(d.seconds, 1f)));
                if (!string.IsNullOrEmpty(d.moodlet)) sim.AddMoodlet(d.moodlet, d.moodValue, d.moodSeconds);
                sim.Report(d.wish ?? d.id);
                if (d.job && payAccum >= 1f) { Household.Earn(Mathf.RoundToInt(payAccum), $"{displayName}'s pay"); GameAudio.Play(GameAudio.Sfx.Ding); }
            }
            payAccum = 0f;
        }

        // ---- autonomy: people look after their own needs

        bool JobHours() { var dn = DayNightCycle.Instance; return dn && dn.hour > 9f && dn.hour < 17f; }

        static float Flat(Vector3 a, Vector3 b) { a.y = b.y; return (a - b).sqrMagnitude; }

        bool TryList(List<(Interactable it, InteractionDef d, float score)> list)
        {
            list.Sort((a, b) => b.score.CompareTo(a.score));
            for (int i = 0; i < list.Count && i < 6; i++)
                if (StartInteraction(list[i].it, list[i].d)) return true;
            return false;
        }

        bool TryFulfil(Need need)
        {
            var list = new List<(Interactable, InteractionDef, float)>();
            foreach (var it in Interactable.All)
            {
                if (!it || it.user != null || !it.gameObject.activeInHierarchy) continue;
                foreach (var d in it.defs)
                {
                    if (d.job || !d.Fills(need) || d.Total(need) < 12f) continue;
                    if (d.cost > 0 && !Household.CanAfford(d.cost + 50)) continue;
                    if (Mathf.Abs(it.transform.position.y - transform.position.y) > 2.5f) continue;    // same floor
                    float dist = Mathf.Sqrt(Flat(it.transform.position, transform.position));
                    list.Add((it, d, d.Total(need) * 0.6f - dist * 1.3f + Random.Range(0f, 8f)));
                }
            }
            return TryList(list);
        }

        bool TryFun()
        {
            var list = new List<(Interactable, InteractionDef, float)>();
            foreach (var it in Interactable.All)
            {
                if (!it || it.user != null || !it.gameObject.activeInHierarchy) continue;
                foreach (var d in it.defs)
                {
                    if (d.job || d.cost > 0 || d.id == "toilet" || d.id == "sleep" || d.id == "chairsit") continue;
                    if (Mathf.Abs(it.transform.position.y - transform.position.y) > 2.5f) continue;
                    if (d.id == "swim" && DayNightCycle.Instance && DayNightCycle.Instance.Night01 > 0.5f) continue;
                    float want = 0f;
                    foreach (var f in d.fills) if (f.perSecond > 0f) want += Mathf.Max(0f, 80f - sim.needs[(int)f.need]) * f.perSecond * d.seconds * 0.02f;
                    if (d.skill.HasValue) want += 4f;
                    if (sim.Has("Bookworm") && d.id == "read") want += 15f;
                    if (sim.Has("Creative") && (d.id == "draw" || d.id == "stargaze")) want += 12f;
                    if (sim.Has("Foodie") && d.id == "cook") want += 8f;
                    float dist = Mathf.Sqrt(Flat(it.transform.position, transform.position));
                    list.Add((it, d, want - dist * 0.4f + Random.Range(0f, 10f)));
                }
            }
            return TryList(list);
        }

        bool TryWork()
        {
            var list = new List<(Interactable, InteractionDef, float)>();
            foreach (var it in Interactable.All)
            {
                if (!it || it.user != null || !it.gameObject.activeInHierarchy) continue;
                foreach (var d in it.defs)
                {
                    if (!d.job || !Careers.Allows(sim.job, d.id)) continue;
                    if (sim.job == "Teacher" && it.id == "officedesk" && d.id == "work") continue;      // teachers work at the teacher's desk
                    if (sim.job == "Engineer" && it.id == "teacherdesk") continue;
                    list.Add((it, d, 10f - Mathf.Sqrt(Flat(it.transform.position, transform.position)) * 0.1f + Random.Range(0f, 3f)));
                }
            }
            return TryList(list);
        }

        bool TryFeedPet()
        {
            foreach (var c in All)
            {
                if (!c.isPet || c.sim == null || c.sim.Get(Need.Hunger) > 45f || !Household.CanAfford(5)) continue;
                if (GoTo(c.transform.position, 1.4f)) { partner = c; mode = Mode.PetWalk; feeding = true; return true; }
            }
            return false;
        }

        /// <summary>A grumble or a sigh when a need is nearly empty, now and then.</summary>
        void NoteFeelings()
        {
            if (Time.time < nextGrumble || sim == null) return;
            float v; var n = sim.Lowest(out v);
            if (v > 22f) return;
            nextGrumble = Time.time + 45f;
            string[] text = { "I'm so hungry...", "I really need the bathroom!", "I could fall asleep standing up.", "I'm bored, bored, bored.", "I feel a bit lonely.", "I need a wash." };
            Say(text[(int)n], 3.5f);
            if (Selected()) { GameAudio.Play(GameAudio.Sfx.Alert); Household.Toast($"{displayName}: {text[(int)n]}"); }
        }

        // ---------- talking and petting ----------

        /// <summary>Someone else to talk to: the closest friend first, among those who are free (not always the same person).</summary>
        Character OtherPerson()
        {
            Character best = null; float bestScore = float.MinValue;
            foreach (var c in All)
            {
                if (c == this || c.isPet || !c.CanChat) continue;
                float f = sim && sim.friendship.TryGetValue(c.displayName, out var v) ? v : 40f;
                float score = f + Random.Range(0f, 40f) - Vector3.Distance(transform.position, c.transform.position) * 1.5f;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        bool TryChat()
        {
            var o = OtherPerson();
            if (o == null || o.onOrder || o.mode == Mode.Waiting || o.mode == Mode.Sitting || o.mode == Mode.Using || o.mode == Mode.Rising || o.mode == Mode.Chatting || o.mode == Mode.ChatWalk || o.mode == Mode.Interacting) return false;   // busy: not pulled into a chat
            if (!GoTo(o.transform.position, 1.6f)) return false;
            partner = o; mode = Mode.ChatWalk;
            return true;
        }

        void TickChatWalk()
        {
            if (partner == null || partner.mode == Mode.Using || partner.mode == Mode.Sitting || partner.mode == Mode.Interacting) { SetIdle(1f); return; }
            if (Vector3.Distance(transform.position, partner.transform.position) < 1.5f)
            {
                agent.ResetPath();
                lineIndex = 0; nextLine = 0f; timer = 12f; mode = Mode.Chatting;
                partner.JoinChat(this);
                return;
            }
            if (!agent.pathPending && agent.remainingDistance < 0.3f) GoTo(partner.transform.position, 1.6f);
        }

        void JoinChat(Character other)
        {
            if (mode == Mode.Interacting) FinishInteraction(false);   // (and out of the shower first)
            partner = other;
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            timer = 12f; lineIndex = 1; nextLine = 2.4f; mode = Mode.Chatting;
        }

        void TickChatting()
        {
            timer -= Time.deltaTime;
            if (partner == null) { SetIdle(1f); return; }
            var d = partner.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 6f);
            rig.pose = Speaking ? CharacterRig.Pose.Talk : CharacterRig.Pose.Stand;
            nextLine -= Time.deltaTime;
            if (nextLine <= 0f && lineIndex % 2 == 0)
            {
                Bubble = Lines[Random.Range(0, Lines.Length)];
                bubbleUntil = Time.time + 3.2f;
                nextLine = 6f;
                lineIndex++;
                partner.nextLine = 3.2f;
            }
            else if (nextLine <= 0f)
            {
                lineIndex++;
                nextLine = 3.4f;
                if (lineIndex % 2 == 1) { Bubble = Lines[Random.Range(0, Lines.Length)]; bubbleUntil = Time.time + 3.2f; }
            }
            if (timer <= 0f)
            {
                if (partner && sim && partner.sim)
                {
                    sim.Give(Need.Social, 38f); sim.Give(Need.Fun, 8f); sim.Befriend(partner.displayName, 6f);
                    sim.relation.TryGetValue(partner.displayName, out var rel);
                    if (rel == "Married" || rel == "Partners") sim.AddMoodlet("Time with my " + (rel == "Married" ? "spouse" : "partner"), 14f, 360f);
                    else sim.AddMoodlet("Nice chat", 10f, 300f);
                    sim.Report("chat");
                    if (Selected()) Household.Toast($"{displayName} had a nice chat with {partner.displayName}.");
                }
                partner = null; SetIdle(Random.Range(1f, 3f));
            }
        }

        bool Selected() => LiveMode.Selected == this;

        bool TryPet()
        {
            foreach (var c in All)
            {
                if (!c.isPet || c.mode == Mode.Waiting || c.mode == Mode.BeingPetted || c.Napping) continue;   // a sleeping pet is left to sleep
                if (!GoTo(c.transform.position, 1.2f)) continue;
                partner = c; mode = Mode.PetWalk;
                return true;
            }
            return false;
        }

        void TickPetWalk()
        {
            if (partner == null) { SetIdle(1f); return; }
            if (Vector3.Distance(transform.position, partner.transform.position) < 0.9f)
            {
                agent.ResetPath();
                timer = 6f; mode = Mode.Petting;
                partner.BePetted(this);
                return;
            }
            if (!agent.pathPending && agent.remainingDistance < 0.3f) GoTo(partner.transform.position, 1.2f);
        }

        void TickPetting()
        {
            timer -= Time.deltaTime;
            if (partner == null) { SetIdle(1f); return; }
            var d = partner.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 6f);
            rig.pose = CharacterRig.Pose.Crouch;
            if (timer <= 0f)
            {
                if (partner)
                {
                    if (sim && partner.sim)
                    {
                        sim.Give(Need.Fun, 16f); sim.Befriend(partner.displayName, 5f); sim.Report("pet");
                        partner.sim.Give(Need.Fun, 32f); partner.sim.Give(Need.Social, 20f); partner.sim.Befriend(displayName, 5f);
                        if (feeding && Household.Spend(5, "pet food")) { partner.sim.Give(Need.Hunger, 65f); PetThing.Food = PetThing.FullBowl; Household.Toast($"{displayName} fed {partner.displayName} and filled the food bowl."); }
                    }
                    partner.EndPetted();
                }
                feeding = false;
                partner = null; SetIdle(Random.Range(1f, 3f));
            }
        }

        void BePetted(Character by)
        {
            StopPetActivity();
            partner = by;
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            timer = 6.2f; mode = Mode.BeingPetted;
            if (sim) sim.AddMoodlet("Loved being petted", 12f, 240f);
            rig.pose = CharacterRig.Pose.Happy;
            var at = transform.position + Vector3.up * 0.3f;
            bool dog = rig && rig.kind == "dog";
            GameAudio.PlayAt(dog ? "dog_bark" : "cat_meow", at, 0.9f, Random.Range(0.95f, 1.1f));
            purr = GameAudio.PlayAt(dog ? "dog_panting" : "cat_purr", at, 0.8f, 1f, 6f);
        }

        AudioSource purr;

        void EndPetted() { GameAudio.StopAct(purr); purr = null; if (mode == Mode.BeingPetted) SetIdle(Random.Range(1f, 3f)); }

        void TickBeingPetted()
        {
            timer -= Time.deltaTime;
            if (partner != null)
            {
                var d = partner.transform.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 5f);
            }
            if (timer <= 0f) SetIdle(Random.Range(1f, 3f));
        }
    }
}

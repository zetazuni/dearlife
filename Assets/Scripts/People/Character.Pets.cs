using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Dearlife
{
    /// <summary>
    /// What the pets get up to besides wandering, sitting, grooming and sleeping: eating from the food bowl and drinking
    /// (a person feeding a pet fills the bowl), napping in the pet bed, keeping someone company, a mad dash round the
    /// house, playing with the other pet, sunbathing in the garden by day, sniffing about, and asking for food when hungry.
    /// </summary>
    public partial class Character
    {
        enum PetAct { None, Eat, Drink, Nap, Company, Zoomies, Play, Sunbathe, Sniff, Beg }

        PetAct petAct;
        int petStage;               // 0 on the way, 1 doing it, 2 stepping on (bed or bowl), 3 stepping off the bed
        PetThing petThing;
        Vector3 petFrom, petTo;
        Quaternion petFromRot, petToRot;
        float petBlend, petSpeed, petTime;
        int zoomLeft;
        string petDoing = "";

        bool IsDog => rig && rig.kind == "dog";

        /// <summary>A pet curled up in its bed is left to sleep.</summary>
        public bool Napping => mode == Mode.PetActivity && petAct == PetAct.Nap && petStage >= 1 || mode == Mode.Idle && isPet && rig && rig.pose == CharacterRig.Pose.Sleep;

        bool PetDecide()
        {
            if (sim == null) return false;
            float hunger = sim.Get(Need.Hunger), energy = sim.Get(Need.Energy), fun = sim.Get(Need.Fun);
            if (hunger < 45f && PetThing.Food > 0 && StartBowl(false)) return true;
            if (hunger < 28f && StartBeg()) return true;
            if (energy < 30f && StartNap()) return true;
            float r = Random.value;
            if (fun < 50f)
            {
                if (r < 0.35f && StartPlay()) return true;
                if (r < 0.6f && StartZoomies()) return true;
                if (StartCompany()) return true;
            }
            bool day = !DayNightCycle.Instance || DayNightCycle.Instance.Night01 < 0.3f;
            if (r < 0.10f && StartBowl(true)) return true;
            if (r < 0.22f && StartCompany()) return true;
            if (r < 0.32f && day && StartSunbathe()) return true;
            if (r < 0.42f && StartSniff()) return true;
            if (r < 0.47f && StartZoomies()) return true;
            if (r < 0.53f && StartPlay()) return true;
            if (r < 0.58f && energy < 70f && StartNap()) return true;
            return false;
        }

        void BeginPet(PetAct a, string label, float seconds)
        {
            petAct = a; petStage = 0; petDoing = label; timer = seconds; petTime = 0f;
            mode = Mode.PetActivity;
        }

        // ---- the things to do

        bool StartBowl(bool water)
        {
            var b = PetThing.Nearest(PetThing.Kind.Bowls, transform.position, this);
            if (!b) return false;
            petThing = b;
            petTo = b.StandAt(water, IsDog ? 0.32f : 0.27f);
            if (!GoTo(petTo, 0.8f)) { petThing = null; return false; }
            b.user = this;
            BeginPet(water ? PetAct.Drink : PetAct.Eat, water ? "Drinking" : "Eating", water ? 5f : 8f);
            return true;
        }

        bool StartNap()
        {
            var bed = PetThing.Nearest(PetThing.Kind.Bed, transform.position, this);
            if (!bed || !GoTo(bed.BedFront, 1.0f)) return false;
            petThing = bed; bed.user = this;
            BeginPet(PetAct.Nap, "Napping in the pet bed", Random.Range(30f, 60f));
            return true;
        }

        Character RandomPerson()
        {
            var people = new List<Character>();
            foreach (var c in All) if (c && !c.isPet && c.transform.position.y < 2.5f && c.mode != Mode.Waiting) people.Add(c);
            return people.Count > 0 ? people[Random.Range(0, people.Count)] : null;
        }

        bool StartCompany()
        {
            var p = RandomPerson();
            if (p == null || !GoTo(p.transform.position, 1.4f)) return false;
            partner = p;
            BeginPet(PetAct.Company, "Keeping " + p.displayName + " company", Random.Range(10f, 18f));
            return true;
        }

        bool StartBeg()
        {
            var p = RandomPerson();
            if (p == null || !GoTo(p.transform.position, 1.4f)) return false;
            partner = p;
            BeginPet(PetAct.Beg, "Asking for food", 6f);
            return true;
        }

        bool StartZoomies()
        {
            zoomLeft = Random.Range(2, 4);
            if (!ZoomOn()) return false;
            petSpeed = agent.speed;
            agent.speed = petSpeed * 2.3f;
            BeginPet(PetAct.Zoomies, IsDog ? "Running laps" : "Having a mad dash", 3f);
            return true;
        }

        bool ZoomOn()
        {
            for (int t = 0; t < 8; t++)
            {
                var d = Random.insideUnitCircle.normalized * Random.Range(3f, 6f);
                if (GoTo(transform.position + new Vector3(d.x, 0f, d.y), 1.5f)) return true;
            }
            return false;
        }

        bool StartPlay()
        {
            foreach (var c in All)
            {
                if (c == this || !c.isPet || c.mode == Mode.Waiting || c.mode == Mode.BeingPetted || c.Napping || c.mode == Mode.PetActivity) continue;
                if (!GoTo(c.transform.position, 1.2f)) continue;
                partner = c;
                BeginPet(PetAct.Play, "Playing with " + c.displayName, 7f);
                return true;
            }
            return false;
        }

        bool StartSunbathe()
        {
            for (int t = 0; t < 8; t++)
            {
                var p = new Vector3(Random.Range(0.5f, 14f), 0f, Random.Range(11f, 18f));   // the garden in front of the house
                if (GoTo(p, 1.5f))
                {
                    BeginPet(PetAct.Sunbathe, "Sunbathing", Random.Range(20f, 35f));
                    return true;
                }
            }
            return false;
        }

        bool StartSniff()
        {
            for (int t = 0; t < 6; t++)
            {
                var d = Random.insideUnitCircle * 4f;
                if (GoTo(transform.position + new Vector3(d.x, 0f, d.y), 1.2f))
                {
                    BeginPet(PetAct.Sniff, "Sniffing around", Random.Range(3f, 6f));
                    return true;
                }
            }
            return false;
        }

        /// <summary>Another pet came over to play: join in where we stand.</summary>
        void JoinPlay(Character other)
        {
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            partner = other;
            BeginPet(PetAct.Play, "Playing with " + other.displayName, 7f);
            petStage = 1;
        }

        // ---- every frame

        void TickPetActivity()
        {
            float dt = Time.deltaTime;
            petTime += dt;
            switch (petStage)
            {
                case 0: PetWalking(); break;
                case 1: PetDoing(dt); break;
                case 2:
                case 3:
                    petBlend = Mathf.Min(1f, petBlend + dt / 0.7f);
                    float e = petBlend * petBlend * (3f - 2f * petBlend);
                    transform.SetPositionAndRotation(Vector3.Lerp(petFrom, petTo, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * (petStage == 2 && petAct == PetAct.Nap ? 0.12f : 0f)),
                                                     Quaternion.Slerp(petFromRot, petToRot, e));
                    rig.pose = CharacterRig.Pose.Walk; rig.walkSpeed = 0.4f;
                    if (petBlend >= 1f)
                    {
                        if (petStage == 2) { petStage = 1; StartDoing(); }
                        else EndPet(Random.Range(1f, 3f));
                    }
                    break;
            }
        }

        void PetWalking()
        {
            float v = agent.enabled ? agent.velocity.magnitude : 0f;
            rig.pose = v > 0.12f ? CharacterRig.Pose.Walk : CharacterRig.Pose.Stand;
            rig.walkSpeed = v;
            if (petTime > 40f) { EndPet(1f); return; }   // could not get there
            if (partner != null && (petAct == PetAct.Company || petAct == PetAct.Beg || petAct == PetAct.Play))
            {
                float near = petAct == PetAct.Play ? 0.8f : 0.95f;
                if (Vector3.Distance(transform.position, partner.transform.position) < near)
                {
                    agent.ResetPath();
                    if (petAct == PetAct.Play)
                    {
                        if (partner.mode == Mode.PetActivity || partner.mode == Mode.BeingPetted || partner.Napping) { EndPet(1f); return; }
                        partner.JoinPlay(this);
                    }
                    petStage = 1; StartDoing();
                    return;
                }
                if (!agent.pathPending && agent.remainingDistance < 0.3f) GoTo(partner.transform.position, 1.4f);
                return;
            }
            if (!Arrived()) return;
            switch (petAct)
            {
                case PetAct.Zoomies:
                    if (--zoomLeft > 0 && ZoomOn()) return;
                    agent.speed = petSpeed;
                    petStage = 1; StartDoing();
                    return;
                case PetAct.Eat:
                case PetAct.Drink:
                    // the last few centimetres exactly, face in the bowl
                    StepTo(petTo, Quaternion.LookRotation(Flat(petThing.Bowl(petAct == PetAct.Drink) - petTo)));
                    return;
                case PetAct.Nap:
                    StepTo(petThing.BedTop, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    return;
                default:
                    petStage = 1; StartDoing();
                    return;
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v : Vector3.forward; }

        /// <summary>Off the walking surface for a moment: a slow step to an exact place (in front of a bowl, onto the bed).</summary>
        void StepTo(Vector3 p, Quaternion rot)
        {
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
            petFrom = transform.position; petFromRot = transform.rotation;
            petTo = p; petToRot = rot; petBlend = 0f;
            petStage = 2;
        }

        void StartDoing()
        {
            switch (petAct)
            {
                case PetAct.Eat: rig.pose = CharacterRig.Pose.Eat; break;
                case PetAct.Drink: rig.pose = CharacterRig.Pose.Drink; break;
                case PetAct.Nap: rig.pose = CharacterRig.Pose.Sleep; break;
                case PetAct.Company: rig.pose = IsDog ? CharacterRig.Pose.Lie : CharacterRig.Pose.Sit; break;
                case PetAct.Beg:
                    rig.pose = IsDog ? CharacterRig.Pose.Sit : CharacterRig.Pose.Happy;
                    Say(IsDog ? "Woof! Woof!" : "Meow?", 3f);
                    GameAudio.PlayAt(IsDog ? "dog_bark" : "cat_meow", transform.position + Vector3.up * 0.3f, 0.8f, Random.Range(0.95f, 1.1f));
                    break;
                case PetAct.Zoomies:
                case PetAct.Play: rig.pose = CharacterRig.Pose.Exercise; break;
                case PetAct.Sunbathe: rig.pose = CharacterRig.Pose.Lie; break;
                case PetAct.Sniff: rig.pose = CharacterRig.Pose.Eat; break;
            }
        }

        void PetDoing(float dt)
        {
            timer -= dt;
            rig.walkSpeed = 0f;
            if (petAct == PetAct.Nap && petThing)
            {
                // once curled up, shuffle round so the body (not the tip of the tail) is in the middle of the cushion
                var off = petThing.BedTop - rig.BodyCentre; off.y = 0f;
                if (petTime > 1.5f && off.sqrMagnitude > 1e-4f) transform.position += off * Mathf.Min(1f, dt * 2f);
            }
            if (partner != null && (petAct == PetAct.Company || petAct == PetAct.Beg || petAct == PetAct.Play))
            {
                var d = Flat(partner.transform.position - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), dt * 4f);
                if (Vector3.Distance(transform.position, partner.transform.position) > 2.2f) timer = Mathf.Min(timer, 0f);   // they walked off
                if (petAct == PetAct.Play) rig.pose = Mathf.Repeat(petTime, 2.4f) < 1.4f ? CharacterRig.Pose.Exercise : CharacterRig.Pose.Happy;
            }
            if (timer > 0f) return;

            // done: what it did for them
            if (sim)
                switch (petAct)
                {
                    case PetAct.Eat: sim.Give(Need.Hunger, 45f); PetThing.Food = PetThing.Food - 1; sim.AddMoodlet("Full tummy", 8f, 240f); break;
                    case PetAct.Drink: sim.Give(Need.Hunger, 6f); sim.Give(Need.Fun, 4f); break;
                    case PetAct.Nap: sim.Give(Need.Energy, 70f); sim.AddMoodlet("Cosy nap", 8f, 300f); break;
                    case PetAct.Company:
                        sim.Give(Need.Fun, 14f);
                        if (partner && partner.sim) { sim.Befriend(partner.displayName, 3f); partner.sim.Befriend(displayName, 3f); partner.sim.AddMoodlet(displayName + " kept me company", 6f, 240f); }
                        break;
                    case PetAct.Zoomies: sim.Give(Need.Fun, 22f); sim.Give(Need.Energy, -6f); break;
                    case PetAct.Play: sim.Give(Need.Fun, 30f); sim.Give(Need.Energy, -4f); if (partner) sim.Befriend(partner.displayName, 4f); break;
                    case PetAct.Sunbathe: sim.Give(Need.Energy, 14f); sim.Give(Need.Fun, 10f); sim.AddMoodlet("Warm fur", 6f, 200f); break;
                    case PetAct.Sniff: sim.Give(Need.Fun, 6f); break;
                }
            if (petAct == PetAct.Nap && petThing)
            {
                // step back down off the bed
                petFrom = transform.position; petFromRot = transform.rotation;
                petTo = petThing.BedFront; petToRot = Quaternion.LookRotation(Flat(petTo - petThing.transform.position)); petBlend = 0f;
                petStage = 3;
                return;
            }
            EndPet(Random.Range(1f, 4f));
        }

        void PetLand(Vector3 p)
        {
            agent.enabled = true;
            var filter = new NavMeshQueryFilter { agentTypeID = DearlifeNav.AgentType, areaMask = agent.areaMask };
            if (NavMesh.SamplePosition(p, out var hit, 1.5f, filter)) p = hit.position;
            agent.Warp(p);
            transform.position = p;
        }

        void EndPet(float idle) => SetIdle(idle);   // SetIdle tidies up through ReleasePet

        /// <summary>Lets go of the bowl or bed, puts the pet back on its feet on the walking surface and its speed back.</summary>
        void ReleasePet()
        {
            if (petThing && petThing.user == this) petThing.user = null;
            if (petAct == PetAct.Zoomies && petSpeed > 0f) agent.speed = petSpeed;
            if (!agent.enabled)
                PetLand(petAct == PetAct.Nap && petThing && petStage != 3 ? petThing.BedFront : transform.position);
            petThing = null; petAct = PetAct.None; petDoing = ""; partner = null;
        }

        /// <summary>Stops whatever the pet is doing right now (somebody came to pet it, a trip to another lot).</summary>
        void StopPetActivity()
        {
            if (mode == Mode.PetActivity) SetIdle(0f);
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Dearlife
{
    /// <summary>Adopting a dog from the shop (Pets). Dogs are kept between sessions and live wherever the household is.</summary>
    public class PetShop : MonoBehaviour
    {
        public static PetShop Instance { get; private set; }
        public GameObject dogPrefab;
        static string Key => SaveSystem.Key("dearlife.dogs");
        static readonly string[] Names = { "Biscuit", "Mochi", "Coco", "Waffles", "Pepper" };
        public const int Price = 1500;
        int count;

        void Awake() { Instance = this; }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(3f);          // the walkable ground is baked by now
            int n = PlayerPrefs.GetInt(Key, 0);
            for (int i = 0; i < n; i++) Spawn(i);
            count = n;
        }

        public static int Adopted => Instance ? Instance.count : 0;

        /// <summary>Pays and brings a dog home. Returns false when there is no money or no room for another.</summary>
        public bool Adopt()
        {
            if (count >= Names.Length) { Household.Toast("Your home is full of dogs already."); return false; }
            if (!Household.Spend(Price, "adopting a dog")) { GameAudio.Play(GameAudio.Sfx.No); return false; }
            var ch = Spawn(count);
            count++;
            PlayerPrefs.SetInt(Key, count);
            GameAudio.Play(GameAudio.Sfx.Buy);
            if (ch) GameAudio.PlayAt("dog_bark", ch.transform.position + Vector3.up * 0.4f, 0.8f, 1f);
            Household.Toast($"{Names[count - 1]} the dog joined the family!");
            return true;
        }

        Character Spawn(int index)
        {
            if (!dogPrefab) return null;
            var lot = LotManager.Current;
            var at = lot != null ? lot.spawn : new Vector3(3.2f, 0f, 6.2f);
            at += new Vector3(-1.4f - 0.6f * index, 0.02f, -1.2f);
            var go = Instantiate(dogPrefab, at, Quaternion.identity);
            go.SetActive(false);                                  // set it all up first, then it wakes up in one go
            go.name = "dog" + (index == 0 ? "" : " " + index);
            var rig = go.AddComponent<CharacterRig>();
            rig.pet = true; rig.kind = "dog";
            go.AddComponent<NavMeshAgent>();
            go.AddComponent<DoorOpener>();
            var ch = go.AddComponent<Character>();
            ch.displayName = Names[index]; ch.isPet = true; ch.scale = 1f;
            go.SetActive(true);
            return ch;
        }
    }
}

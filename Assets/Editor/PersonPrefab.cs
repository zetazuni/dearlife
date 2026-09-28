using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// Makes Resources/People/Person.prefab: the MPFB2 person (mpfb_test) with everything a created person needs to be
    /// shaped, dressed and animated (BodyShape, Eyes, Wardrobe, CharacterRig for the MPFB skeleton, PersonLook). The
    /// character creator and <see cref="Residents"/> make every created person from it. Run again after the model changes.
    /// </summary>
    public static class PersonPrefab
    {
        const string Model = "Assets/Art/Models/Characters/mpfb_test.fbx";
        const string Shape = "Assets/Art/Models/Characters/mpfb_test.bodyshape.json";
        const string Path = "Assets/Resources/People/Person.prefab";

        [MenuItem("Dearlife/Make person prefab")]
        public static void Make()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (!model) { Debug.LogWarning("Dearlife: " + Model + " not found."); return; }
            Directory.CreateDirectory("Assets/Resources/People");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = "Person";
            var bs = go.AddComponent<BodyShape>();
            bs.shapeData = AssetDatabase.LoadAssetAtPath<TextAsset>(Shape);
            go.AddComponent<Eyes>().eyeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Characters/mpfb_test_eye.mat");
            go.AddComponent<Wardrobe>().outfit = "";
            go.AddComponent<CharacterRig>().kind = "mpfb";
            go.AddComponent<PersonLook>();
            PrefabUtility.SaveAsPrefabAsset(go, Path);
            Object.DestroyImmediate(go);
            Debug.Log("Dearlife: made " + Path);
        }
    }
}

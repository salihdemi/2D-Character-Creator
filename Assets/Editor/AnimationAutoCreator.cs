using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public class AnimationAutoCreator : EditorWindow
{
    private Texture2D spriteSheet;
    private string childName = "Hair"; // Animasyonun hedefleyeceði child objenin ismi
    private string partID = "01";      // SO ID'si ve dosya isimlendirmesi için
    private string animSavePath = "Assets/Animations";
    private string soSavePath = "Assets/ScriptableObjects";

    [MenuItem("Tools/Animation Creator")]
    public static void ShowWindow()
    {
        GetWindow<AnimationAutoCreator>("Anim & SO Creator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Sprite Animasyon ve SO Oluþturucu", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        spriteSheet = (Texture2D)EditorGUILayout.ObjectField("Sprite Sheet", spriteSheet, typeof(Texture2D), false);

        EditorGUILayout.Space();
        childName = EditorGUILayout.TextField("Child Obje Ýsmi", childName);
        partID = EditorGUILayout.TextField("Parça ID (Sayý veya Kod)", partID);

        EditorGUILayout.Space();
        DrawPathSelector("Animasyon Ana Klasörü", ref animSavePath);
        DrawPathSelector("SO Kayýt Yolu", ref soSavePath);

        EditorGUILayout.Space();

        if (GUILayout.Button("Animasyonlarý ve SO'yu Oluþtur") && spriteSheet != null)
        {
            CreateAll();
        }
    }

    private void DrawPathSelector(string label, ref string path)
    {
        EditorGUILayout.BeginHorizontal();
        path = EditorGUILayout.TextField(label, path);
        if (GUILayout.Button("Seç", GUILayout.Width(50)))
        {
            string folder = EditorUtility.OpenFolderPanel(label, "Assets", "");
            if (!string.IsNullOrEmpty(folder))
            {
                if (folder.StartsWith(Application.dataPath))
                    path = "Assets" + folder.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void CreateAll()
    {
        string path = AssetDatabase.GetAssetPath(spriteSheet);
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        List<Sprite> spriteList = new List<Sprite>();

        foreach (var obj in assets) { if (obj is Sprite s) spriteList.Add(s); }
        spriteList.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));

        if (spriteList.Count < 32)
        {
            Debug.LogError("Hata: Sprite sheet'te yeterli kare yok (32 kare gerekli)!");
            return;
        }

        // 1. Klasörleri Hazýrla (Ýsimlendirme için partID kullanýlýyor)
        string folderName = childName + "_" + partID;
        string specificAnimPath = Path.Combine(animSavePath, folderName).Replace("\\", "/");

        if (!Directory.Exists(specificAnimPath)) Directory.CreateDirectory(specificAnimPath);
        if (!Directory.Exists(soSavePath)) Directory.CreateDirectory(soSavePath);

        // 2. Animasyonlarý Oluþtur
        List<AnimationClip> createdClips = new List<AnimationClip>();
        string[] actions = { "Walk", "Walk", "Walk", "Walk", "Idle", "Idle", "Idle", "Idle" };
        string[] directions = { "Down", "Up", "Left", "Right", "Down", "Up", "Left", "Right" };

        for (int i = 0; i < 8; i++)
        {
            // Dosya ismi: Shirt_01_Walk_Down.anim gibi
            string animName = $"{childName}_{partID}_{actions[i]}_{directions[i]}.anim";
            AnimationClip clip = new AnimationClip();
            clip.frameRate = 8;

            EditorCurveBinding curveBinding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = childName, // Animasyonun kod içinde hedefleyeceði child obje
                propertyName = "m_Sprite"
            };

            ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[4];
            for (int j = 0; j < 4; j++)
            {
                keyframes[j] = new ObjectReferenceKeyframe
                {
                    time = j * (1f / clip.frameRate),
                    value = spriteList[i * 4 + j]
                };
            }
            AnimationUtility.SetObjectReferenceCurve(clip, curveBinding, keyframes);

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string finalAnimPath = Path.Combine(specificAnimPath, animName).Replace("\\", "/");
            AssetDatabase.CreateAsset(clip, finalAnimPath);
            createdClips.Add(clip);
        }

        // 3. ScriptableObject Oluþtur
        CreateBodyPartSO(createdClips);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Baþarýlý", $"Animasyonlar '{childName}' objesini hedefleyecek þekilde oluþturuldu.", "Tamam");
    }

    private void CreateBodyPartSO(List<AnimationClip> clips)
    {
        SO_BodyPart newSO = ScriptableObject.CreateInstance<SO_BodyPart>();

        // SO içindeki görünen isim (Örn: Hair 05)
        newSO.bodyPartName = childName + " " + partID;

        // ID: partID içindeki sayýlarý çeker
        int id = 0;
        string idStr = Regex.Match(partID, @"\d+").Value;
        if (!string.IsNullOrEmpty(idStr)) int.TryParse(idStr, out id);
        newSO.bodyPartAnimationID = id;

        newSO.allBodyPartAnimations = new List<AnimationClip>(clips);

        // Dosya ismi: SO_Hair_01.asset gibi
        string soFileName = $"SO_{childName}_{partID}.asset";
        string finalSOPath = Path.Combine(soSavePath, soFileName).Replace("\\", "/");

        AssetDatabase.CreateAsset(newSO, finalSOPath);
        EditorUtility.SetDirty(newSO);
    }
}
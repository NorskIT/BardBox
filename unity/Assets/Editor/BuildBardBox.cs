using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BuildBardBox
{
    public static void Build()
    {
        ExportSongs();
        var models=AssetDatabase.FindAssets("t:Model",new[]{"Assets/Model"})
            .Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".obj",StringComparison.OrdinalIgnoreCase)).ToArray();
        if(models.Length!=1)throw new InvalidOperationException("Put exactly one imported FBX or OBJ boombox model, with textures, under unity/Assets/Model. No replacement model is generated.");
        Directory.CreateDirectory("Assets/Generated");
        var root=new GameObject("BardBoxVisual");
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(models[0]));
        model.transform.SetParent(root.transform,false);
        // Supplied FBX speakers face -Z; the held box should face the player's +Z.
        model.transform.localRotation=Quaternion.Euler(0,180,0)*model.transform.localRotation;
        var renderers=root.GetComponentsInChildren<Renderer>();
        foreach(var r in renderers)foreach(var mat in r.sharedMaterials)
            Debug.Log("BardBox model material: "+(mat?mat.name+" / "+mat.shader.name:"MISSING"));
        if(renderers.Length==0)throw new InvalidOperationException("The model has no renderers.");
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        if(bounds.size.x<.001f)throw new InvalidOperationException("Model has invalid bounds.");
        float scale=.6f/bounds.size.x;
        model.transform.localScale*=scale;
        // Right hand grips the right underside of a 60 cm box; left hand is 50 cm away.
        model.transform.localPosition=new Vector3(-.25f,.18f,0)-bounds.center*scale;
        foreach(var collider in root.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
        string prefabPath="Assets/Generated/BardBoxVisual.prefab";
        PrefabUtility.SaveAsPrefabAsset(root,prefabPath);UnityEngine.Object.DestroyImmediate(root);
        const string materialPath="Assets/Generated/ShockwaveMaterial.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!material){material=new Material(Shader.Find("BardBox/MusicRing"));AssetDatabase.CreateAsset(material,materialPath);}
        AssetDatabase.SaveAssets();
        var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../assets/bundle"));Directory.CreateDirectory(output);
        var target=Environment.GetCommandLineArgs().Contains("-bardboxLinux")?BuildTarget.StandaloneLinux64:BuildTarget.StandaloneWindows64;
        var builds=new[]{new AssetBundleBuild {assetBundleName="bardbox.assets",assetNames=new[]{prefabPath,materialPath},addressableNames=new[]{"BardBoxVisual","ShockwaveMaterial"}}};
        if(!BuildPipeline.BuildAssetBundles(output,builds,BuildAssetBundleOptions.ChunkBasedCompression,target))throw new InvalidOperationException("Asset bundle build failed.");
        Debug.Log("BardBox asset bundle built: "+output);
    }

    private static void ExportSongs()
    {
        var project=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
        var mappings=File.ReadAllLines(Path.Combine(project,"assets/songs.tsv"))
            .Where(l=>!string.IsNullOrWhiteSpace(l)&&!l.StartsWith("#")).Select(l=>l.Split('\t')).ToArray();
        if(mappings.Length!=11 || mappings.Any(m=>m.Length!=7) || mappings.Select(m=>m[0]).Distinct().Count()!=11)
            throw new InvalidOperationException("Expected eleven unique song mappings.");
        foreach(var mapping in mappings)
        {
            var path="Assets/AudioSource/"+mapping[0]+".mp3";
            File.Copy(Path.Combine(project,"assets-source",mapping[1]),path,true);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            if(!importer)throw new InvalidOperationException("Missing supplied song: "+path);
            importer.forceToMono=true;importer.loadInBackground=false;
            var settings=importer.defaultSampleSettings;
            settings.preloadAudioData=true;
            settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;
            settings.sampleRateSetting=AudioSampleRateSetting.OverrideSampleRate;settings.sampleRateOverride=44100;
            importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            clip.LoadAudioData();
            var samples=new float[clip.samples*clip.channels];
            if(!clip.GetData(samples,0))throw new InvalidOperationException("Cannot decode "+path);
            foreach(var id in new[]{mapping[0]})
            {
                var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../assets/audio/"+id+".wav"));
                using(var writer=new BinaryWriter(File.Create(output)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples.Length*2);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);
                    writer.Write((short)1);writer.Write((short)clip.channels);writer.Write(clip.frequency);
                    writer.Write(clip.frequency*clip.channels*2);writer.Write((short)(clip.channels*2));writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples.Length*2);
                    foreach(var sample in samples)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample,-1,1)*32767));
                }
            }
            Debug.Log("BardBox decoded "+mapping[0]+" / "+mapping[2]+": "+clip.length+" seconds, "+clip.channels+" channels at "+clip.frequency+" Hz");
        }
    }
}

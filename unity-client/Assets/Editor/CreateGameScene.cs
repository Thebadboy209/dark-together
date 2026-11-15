#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CreateGameScene
{
    [MenuItem("Dark Together/Generate Playable Scene")]
    public static void Create()
    {
        if(!System.IO.Directory.Exists("Assets/Scenes")) System.IO.Directory.CreateDirectory("Assets/Scenes");
        Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        GameObject camObject=new GameObject("Main Camera");camObject.tag="MainCamera";
        Camera camera=camObject.AddComponent<Camera>();camera.orthographic=true;
        camera.orthographicSize=7.35f;camera.transform.position=new Vector3(11.5f,6.5f,-10);
        GameObject game=new GameObject("DARK TOGETHER - Game Controller");
        game.AddComponent<DarkTogether>();
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/DarkTogether.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/DarkTogether.unity",true)};
        Selection.activeGameObject=game;
        Debug.Log("Scene created. Configure serverUrl (and optional firebaseWebApiKey), then press Play.");
    }
}
#endif

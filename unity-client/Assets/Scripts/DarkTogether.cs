using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Colyseus;
using Colyseus.Schema;
using UnityEngine;
using UnityEngine.Networking;

// Gameplay prototype: 2D top-down, PC keyboard + mobile virtual joystick.
// Depends on Colyseus Unity SDK (MIT): https://github.com/colyseus/colyseus-unity-sdk
public class DarkTogether : MonoBehaviour
{
    [Header("Local server: ws://localhost:2567. Phone: use PC LAN IP or public wss:// URL")]
    public string serverUrl = "ws://127.0.0.1:2567";
    [Header("Optional: Firebase Web API key. Leave empty for LOCAL GUEST mode")]
    public string firebaseWebApiKey = "";

    private Client client;
    private Room<DynamicSchema> room;
    private readonly Dictionary<string, GameObject> avatars = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> lightCones = new Dictionary<string, GameObject>();
    private Sprite square;
    private GameObject worldRoot, monsterView;
    private Camera cam;
    private string[] rows;
    private string inputCode = "";
    private string editableServerUrl = "";
    private string status = "Create a room or join with your friend's code";
    private string phase = "menu";
    private string sessionId = "";
    private bool busy;
    private bool sprintTouch;
    private bool generatorActive, doorActive;
    private float sendTimer;
    private Vector2 joystick;
    private Vector2 localTarget = new Vector2(2, 2);
    private Color backgroundColor = new Color(0.045f, 0.055f, 0.085f);

    [Serializable] private class HospitalMap { public string[] rows; }
    [Serializable] private class AuthResponse { public string idToken; }
    [Serializable] private class InputMessage { public float x; public float y; public bool sprint; }

    void Start()
    {
        Application.targetFrameRate = 60;
        editableServerUrl = PlayerPrefs.GetString("dark_together_server", serverUrl);
        cam = Camera.main;
        if (cam == null)
        {
            GameObject obj = new GameObject("Main Camera"); obj.tag = "MainCamera";
            cam = obj.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 7.35f;
        cam.backgroundColor = backgroundColor;
        cam.transform.position = new Vector3(11.5f, 6.5f, -10f);
        Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[16 * 16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels); tex.Apply(); tex.filterMode = FilterMode.Point;
        square = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
        var asset = Resources.Load<TextAsset>("hospital_map");
        if (asset == null) { status = "Missing Resources/hospital_map.json"; return; }
        rows = JsonUtility.FromJson<HospitalMap>(asset.text).rows;
        MakeWorld();
        Application.runInBackground = true;
    }

    GameObject RectSprite(string name, Vector2 position, Vector2 scale, Color color, int order = 0, Transform parent = null)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y, 0);
        go.transform.localScale = new Vector3(scale.x, scale.y, 1);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = square; renderer.color = color; renderer.sortingOrder = order;
        return go;
    }

    void MakeWorld()
    {
        worldRoot = new GameObject("Hospital - generated from hospital_map.json");
        for (int y = 0; y < rows.Length; y++) for (int x = 0; x < rows[y].Length; x++)
        {
            char t = rows[y][x];
            Color floor = ((x + y) % 2 == 0) ? new Color(.10f, .12f, .16f) : new Color(.115f, .13f, .175f);
            RectSprite("Floor", new Vector2(x, y), Vector2.one, floor, 0, worldRoot.transform);
            if (t == '#')
            {
                RectSprite("Wall", new Vector2(x, y), Vector2.one, new Color(.27f, .23f, .33f), 2, worldRoot.transform);
                RectSprite("WallHighlight", new Vector2(x, y + .40f), new Vector2(.95f, .10f), new Color(.41f, .35f, .43f), 3, worldRoot.transform);
            }
            else if (t == 'G')
                RectSprite("Generator [G]", new Vector2(x, y), new Vector2(.75f, .75f), new Color(.83f, .52f, .21f), 5, worldRoot.transform);
            else if (t == 'D')
                RectSprite("Gate [D]", new Vector2(x, y), new Vector2(.85f, .8f), new Color(.8f, .27f, .35f), 5, worldRoot.transform);
            else if (t == 'X')
                RectSprite("Exit [X]", new Vector2(x, y), new Vector2(.85f, .85f), new Color(.17f, .73f, .56f), 4, worldRoot.transform);
        }
        monsterView = RectSprite("The Shadow", new Vector2(18, 3), new Vector2(.8f, .85f), new Color(.85f, .12f, .19f), 19);
        RectSprite("Shadow's eye", new Vector2(0, .13f), new Vector2(.38f, .10f), Color.white, 20, monsterView.transform).transform.localPosition = new Vector3(0, .13f, 0);
    }

    GameObject CreateAvatar(string id, bool isSelf)
    {
        GameObject go = RectSprite(isSelf ? "YOU" : "FRIEND", new Vector2(2, 2), new Vector2(.64f, .72f),
          isSelf ? new Color(.43f, .86f, .93f) : new Color(.91f, .76f, .38f), 21);
        RectSprite("Face", Vector2.zero, new Vector2(.29f, .14f), backgroundColor, 22, go.transform)
            .transform.localPosition = new Vector3(0, .11f, 0);
        lightCones[id] = MakeCone(go.transform);
        return go;
    }

    GameObject MakeCone(Transform parent)
    {
        GameObject cone = new GameObject("Flashlight cone");
        cone.transform.SetParent(parent, false);
        const int segments = 22;
        Vector3[] verts = new Vector3[segments + 2]; Color[] colors = new Color[verts.Length];
        int[] tris = new int[segments * 3];
        verts[0] = Vector3.zero; colors[0] = new Color(.84f, .9f, 1f, .32f);
        for(int i=0;i<=segments;i++)
        {
            float angle = Mathf.Deg2Rad * Mathf.Lerp(-35f, 35f, i / (float)segments);
            verts[i + 1] = new Vector3(Mathf.Cos(angle)*4f, Mathf.Sin(angle)*4f, 0);
            colors[i + 1] = new Color(.78f, .87f, 1f, .01f);
            if(i<segments) {tris[i*3]=0;tris[i*3+1]=i+1;tris[i*3+2]=i+2;}
        }
        Mesh mesh = new Mesh(); mesh.vertices=verts;mesh.colors=colors;mesh.triangles=tris;mesh.RecalculateBounds();
        cone.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer=cone.AddComponent<MeshRenderer>();
        renderer.material=new Material(Shader.Find("Sprites/Default"));
        renderer.sortingLayerName="Default";renderer.sortingOrder=10;
        return cone;
    }

    async Task<string> GetFirebaseToken()
    {
        if(string.IsNullOrWhiteSpace(firebaseWebApiKey)) return ""; // guest mode for local prototype
        string url="https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + firebaseWebApiKey.Trim();
        using(var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{\"returnSecureToken\":true}"));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            var operation=req.SendWebRequest();
            while(!operation.isDone) await Task.Yield();
            if(req.result!=UnityWebRequest.Result.Success)
                throw new Exception("Firebase auth failed: " + req.error + " " + req.downloadHandler.text);
            var answer=JsonUtility.FromJson<AuthResponse>(req.downloadHandler.text);
            if(answer==null || string.IsNullOrEmpty(answer.idToken)) throw new Exception("Firebase returned no ID token");
            return answer.idToken;
        }
    }

    async void Connect(bool create)
    {
        if(busy || room!=null) return;
        busy=true;status="Connecting...";
        try
        {
            string proposedUrl = editableServerUrl.Trim().TrimEnd('/');
            if (!proposedUrl.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) &&
                !proposedUrl.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Server must start with ws:// or wss://");
            serverUrl = proposedUrl;
            PlayerPrefs.SetString("dark_together_server", serverUrl);
            PlayerPrefs.Save();
            string token=await GetFirebaseToken();
            client = new Client(serverUrl);
            var opts = new Dictionary<string, object> { {"displayName", "Player"} };
            if(!string.IsNullOrEmpty(token)) opts["firebaseToken"] = token;
            room = create ? await client.Create<DynamicSchema>("dark_together", opts)
                          : await client.JoinById<DynamicSchema>(inputCode.Trim().ToUpperInvariant(), opts);
            await room.WaitForFirstState();
            sessionId = room.SessionId;phase="lobby";
            status="Connected. Press READY and wait for your partner.";
        }
        catch(Exception e) { Debug.LogError(e);status="Could not join: " + e.Message;room=null;phase="menu"; }
        finally {busy=false;}
    }

    async void Disconnect()
    {
        var old=room;room=null;phase="menu";sessionId="";busy=false;
        foreach(var kv in avatars) if(kv.Value!=null) Destroy(kv.Value);
        avatars.Clear();lightCones.Clear();
        if(old!=null) { try {await old.Leave();} catch(Exception e) {Debug.LogWarning(e.Message);} }
        status="Disconnected";
    }

    Vector2 ReadMovement()
    {
        Vector2 v=Vector2.zero;
        if(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow))v.y+=1;
        if(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow))v.y-=1;
        if(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow))v.x-=1;
        if(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow))v.x+=1;
        joystick=Vector2.zero;
        float unit=Screen.width/1280f;
        float uy=Screen.height/720f;
        Vector2 center=new Vector2(150*unit, 140*uy);
        foreach(Touch t in Input.touches)
        {
            if(t.phase==TouchPhase.Ended || t.phase==TouchPhase.Canceled) continue;
            // The joystick is bottom-left, touch position origin is bottom-left.
            if(t.position.x<Screen.width*.40f && t.position.y<Screen.height*.43f)
            {
                joystick=Vector2.ClampMagnitude(new Vector2((t.position.x-center.x)/(85*unit),
                                    (t.position.y-center.y)/(85*uy)),1);
                break;
            }
        }
        return Vector2.ClampMagnitude(v+joystick,1);
    }

    void Update()
    {
        if(rows==null || room==null) return;
        try
        {
            DynamicSchema state=room.State;
            string nextPhase=state.Get<string>("phase");
            phase=nextPhase;
            generatorActive=state.Get<bool>("generatorOn");
            doorActive=state.Get<bool>("doorOpen");
            status=state.Get<string>("notice");
            var players=state.Get<MapSchema<DynamicSchema>>("players");
            if(players==null) return;
            var seen=new HashSet<string>();
            foreach(var kv in players)
            {
                seen.Add(kv.Key);
                if(!avatars.TryGetValue(kv.Key, out GameObject go))
                {
                    go=CreateAvatar(kv.Key, kv.Key==sessionId);avatars[kv.Key]=go;
                }
                var p=kv.Value;
                Vector3 pos=new Vector3((float)p.Get<double>("x"),(float)p.Get<double>("y"),0);
                go.transform.position=Vector3.Lerp(go.transform.position,pos,Mathf.Clamp01(Time.deltaTime*15));
                if(kv.Key==sessionId) localTarget=pos;
                if(lightCones.TryGetValue(kv.Key,out GameObject cone))
                {
                    cone.SetActive(p.Get<bool>("light") && phase=="playing");
                    Vector2 facing=new Vector2((float)p.Get<double>("dx"),(float)p.Get<double>("dy"));
                    cone.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(facing.y,facing.x)*Mathf.Rad2Deg);
                }
            }
            var removals=new List<string>();
            foreach(var kv in avatars) if(!seen.Contains(kv.Key)) removals.Add(kv.Key);
            foreach(string id in removals) { Destroy(avatars[id]);avatars.Remove(id);lightCones.Remove(id); }
            if(monsterView!=null) monsterView.transform.position=Vector3.Lerp(monsterView.transform.position,
                new Vector3((float)state.Get<double>("monsterX"),(float)state.Get<double>("monsterY"),0),Time.deltaTime*10f);
            // Hide the locked gate visually after the puzzle.
            if(worldRoot!=null) foreach(Transform child in worldRoot.transform)
                if(child.name=="Gate [D]") child.gameObject.SetActive(!doorActive);
            if(phase=="playing")
            {
                Vector2 movement=ReadMovement();
                bool sprint=Input.GetKey(KeyCode.LeftShift)||sprintTouch;
                sendTimer+=Time.deltaTime;
                if(sendTimer>=.05f) {sendTimer=0;room.Send("input",new InputMessage {x=movement.x,y=movement.y,sprint=sprint});}
                if(Input.GetKeyDown(KeyCode.F)) room.Send("light");
                if(Input.GetKeyDown(KeyCode.E)) room.Send("interact");
            }
            if(phase=="playing" && avatars.ContainsKey(sessionId))
            {
                Vector3 target=avatars[sessionId].transform.position;
                float aspect=(float)Screen.width/Math.Max(Screen.height,1);
                cam.orthographicSize=aspect<1.2f?11f:7.35f;
                float horizontalExtent=cam.orthographicSize*aspect;
                float cx=Mathf.Clamp(target.x,horizontalExtent-.5f,rows[0].Length-.5f-horizontalExtent);
                float cy=Mathf.Clamp(target.y,cam.orthographicSize-.5f,rows.Length-.5f-cam.orthographicSize);
                if(float.IsNaN(cx)||float.IsInfinity(cx))cx=11.5f;
                if(float.IsNaN(cy)||float.IsInfinity(cy))cy=6.5f;
                // If full map is smaller than viewport, hold camera in the middle.
                if(horizontalExtent*2>rows[0].Length)cx=(rows[0].Length-1)/2f;
                if(cam.orthographicSize*2>rows.Length)cy=(rows.Length-1)/2f;
                cam.transform.position=Vector3.Lerp(cam.transform.position,new Vector3(cx,cy,-10),Time.deltaTime*5f);
            }
        }
        catch(Exception e) { Debug.LogWarning("Game state processing: "+e.Message); }
    }

    GUIStyle styleTitle, styleText, styleButton, styleBox;
    void SetupStyles()
    {
        if(styleTitle!=null)return;
        styleTitle=new GUIStyle(GUI.skin.label) {fontSize=38,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};
        styleTitle.normal.textColor=new Color(.91f,.94f,1f);
        styleText=new GUIStyle(GUI.skin.label) {fontSize=19,alignment=TextAnchor.MiddleCenter,wordWrap=true};
        styleText.normal.textColor=new Color(.79f,.87f,.95f);
        styleButton=new GUIStyle(GUI.skin.button) {fontSize=21,fontStyle=FontStyle.Bold};
        styleBox=new GUIStyle(GUI.skin.box) {fontSize=18};
    }
    void Send(string key) {if(room!=null) try{room.Send(key);}catch(Exception e){status=e.Message;}}
    void OnGUI()
    {
        SetupStyles();
        var previous=GUI.matrix;
        GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/1280f,Screen.height/720f,1));
        if(phase=="menu")
        {
            GUI.Box(new Rect(380,85,520,525),"",styleBox);
            GUI.Label(new Rect(400,110,480,62),"DARK TOGETHER",styleTitle);
            GUI.Label(new Rect(410,175,460,38),"2D CO-OP HORROR  •  PC + MOBILE",styleText);
            GUI.Label(new Rect(420,223,440,32),"SERVER URL (ws:// or wss://)",styleText);
            editableServerUrl=GUI.TextField(new Rect(430,257,420,36),editableServerUrl,160);
            GUI.enabled=!busy;
            if(GUI.Button(new Rect(460,309,360,62),"CREATE ROOM",styleButton))Connect(true);
            inputCode=GUI.TextField(new Rect(460,390,360,38),inputCode.ToUpperInvariant(),8);
            if(GUI.Button(new Rect(460,439,360,62),"JOIN ROOM",styleButton))Connect(false);
            GUI.enabled=true;
            GUI.Label(new Rect(400,515,480,70),status,styleText);
        }
        else
        {
            GUI.Box(new Rect(16,12,580,112),"",styleBox);
            GUI.Label(new Rect(30,14,540,37),"ROOM: "+(room!=null?room.Id:"-"),styleText);
            GUI.Label(new Rect(28,50,550,62),status,styleText);
            if(GUI.Button(new Rect(1070,18,185,48),"LEAVE",styleButton))Disconnect();
            if(phase=="lobby" && room!=null)
            {
                GUI.Box(new Rect(390,175,500,305),"LOBBY",styleBox);
                var players=room.State.Get<MapSchema<DynamicSchema>>("players");
                int count=players!=null?players.Count:0;
                var self=players!=null && players.ContainsKey(sessionId)?players[sessionId]:null;
                bool isReady=self!=null && self.Get<bool>("ready");
                GUI.Label(new Rect(420,225,440,40),$"PLAYERS: {count}/2",styleText);
                if(GUI.Button(new Rect(440,283,400,60),isReady?"NOT READY":"READY",styleButton))Send("ready");
                if(sessionId==room.State.Get<string>("host") &&
                    GUI.Button(new Rect(440,360,400,60),"START (both READY)",styleButton)) Send("start");
                if(GUI.Button(new Rect(850,185,230,46),"COPY ROOM",styleButton))GUIUtility.systemCopyBuffer=room.Id;
            }
            if(phase=="playing")
            {
                GUI.Label(new Rect(920,90,325,34),"[G] GENERATOR: "+(generatorActive?"ON":"OFF"),styleText);
                GUI.Label(new Rect(920,125,325,34),"[D] GATE: "+(doorActive?"OPEN":"LOCKED"),styleText);
                if(Application.isMobilePlatform)
                {
                    GUI.Box(new Rect(60,465,180,180),"MOVE",styleBox);
                    GUI.Label(new Rect(95+joystick.x*50,520-joystick.y*50,100,52),"●",styleTitle);
                    if(GUI.Button(new Rect(1000,490,190,58),"INTERACT",styleButton))Send("interact");
                    if(GUI.Button(new Rect(1010,560,185,55),"FLASHLIGHT",styleButton))Send("light");
                    sprintTouch=GUI.RepeatButton(new Rect(790,557,184,55),"RUN",styleButton);
                }
                else GUI.Label(new Rect(400,667,490,33),"WASD move • E interact • F flashlight • Shift run",styleText);
            }
            if(phase=="won")
            {
                GUI.Box(new Rect(380,230,520,220),"",styleBox);
                GUI.Label(new Rect(405,260,470,75),"YOU ESCAPED!",styleTitle);
                GUI.Label(new Rect(410,345,460,53),"Both players made it out together.",styleText);
            }
        }
        GUI.matrix=previous;
    }

    async void OnDestroy()
    {
        if(room!=null){try{await room.Leave();}catch { /* app is exiting */ }}
    }
}

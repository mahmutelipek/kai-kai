using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    /// <summary>Resolution-independent shared screen menu and run interface.</summary>
    public sealed partial class RunHud : MonoBehaviour
    {
        BoardController _board;
        RunManager _run;
        GUIStyle _label, _button;
        int _players = 6;
        bool _muted;
        Texture2D _brush, _circle;
        readonly Texture2D[] _portraits = new Texture2D[6];
        readonly Color _ink = new Color(.035f, .065f, .09f, .94f);
        readonly Color _cyan = new Color(.25f, .94f, .87f);
        readonly Color _purple = new Color(.9f, .5f, 1f);
        public void Initialize(BoardController board, RunManager run) { _board = board; _run = run; InitializeMenu(); for(int i=0;i<6;i++) _portraits[i]=Resources.Load<Texture2D>("Art/Portraits/"+RiderArtRig.AssetNames[i]); }
        void Update()
        {
            var kb = Keyboard.current; var pad = Gamepad.current;
            if (_run == null || !_run.SessionEnabled) return;
            if (_run.Phase == RunPhase.Ready) HandleMenuKeys(kb,pad);
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
                Vector2 pixel = mouse.position.ReadValue();
                Vector2 point = new Vector2(pixel.x - (Screen.width - 1600f * scale) / 2f,
                    Screen.height - pixel.y - (Screen.height - 900f * scale) / 2f) / scale;
                ClickAt(point);
            }
            if ((kb != null && kb.enterKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame))
            {
                if (_run.Phase == RunPhase.Ready) ConfirmMenu();
                else if (_run.Phase == RunPhase.Results) StartRun();
                else _run.TogglePause();
            }
        }
        // Input System pointer coordinates match the same 1600×900 canvas used by the renderer.
        public void ClickAt(Vector2 point)
        {
            bool Hit(float x, float y, float w, float h) => new Rect(x,y,w,h).Contains(point);
            if (_run.Phase == RunPhase.Ready)
            {
                ClickMenu(point);
            }
            else if (_run.Phase == RunPhase.Paused)
            {
                if (Hit(546,343,508,64)) _run.TogglePause();
                else if (Hit(546,431,508,64)) StartRun();
                else if (Hit(546,519,508,64)) { _muted=!_muted; GetComponent<RunFeedback>().Muted=_muted; }
                else if (Hit(546,607,508,64)) { _menuPage=MenuPage.Home; _run.ShowMenu(); }
            }
            else if (_run.Phase == RunPhase.Results)
            {
                if (Hit(350,792,430,62)) StartRun();
                else if (Hit(820,792,430,62)) { _menuPage=MenuPage.Home; _run.ShowMenu(); }
            }
        }
        void StartRun() { _board.Simulation.SetActivePlayerCount(_players); _run.RestartRun(); }
        void Panel(Rect r, Color color) { GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
        void Text(Rect r, string value, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            _label.fontSize = size; _label.normal.textColor = color; _label.alignment = alignment;
            GUI.Label(r, value, _label);
        }
        void Button(Rect r, string value, bool primary = false)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Panel(r, primary ? (hover ? Color.white : _cyan) : (hover ? new Color(.17f,.25f,.3f) : new Color(.1f,.16f,.21f)));
            _button.normal.textColor = primary ? _ink : Color.white;
            GUI.Label(r, value, _button);
            // Pointer actions are handled once in Update through the Input System.
        }
        void OnGUI()
        {
            if (_board == null || _run == null) return;
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true };
                _button = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            Matrix4x4 matrix = GUI.matrix;
            if (_run.Phase == RunPhase.Ready && _run.SessionEnabled) DrawMenuBackground();
            else if (_run.Phase == RunPhase.Results) DrawMenuBackground(.62f);
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600 * scale) / 2, (Screen.height - 900 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            if (_run.Phase == RunPhase.Ready && _run.SessionEnabled) Menu();
            else
            {
                if (_run.Phase != RunPhase.Results) Hud();
                if (_run.Phase == RunPhase.Paused) Pause();
                if (_run.Phase == RunPhase.Results) Results();
            }
            GUI.matrix = matrix;
        }
        void EnsureArt()
        {
            if (_brush != null) return;
            _brush=new Texture2D(256,64,TextureFormat.RGBA32,false); _circle=new Texture2D(64,64,TextureFormat.RGBA32,false);
            for(int y=0;y<64;y++) for(int x=0;x<256;x++)
            {
                int left=8+(63-y)/5+((y/4)*17%7), right=246-y/7+((y/3)*11%9);
                bool ink=x>=left&&x<=right&&y>2+((x/13)*7%4)&&y<61-((x/11)*13%4);
                _brush.SetPixel(x,y,ink?Color.white:Color.clear);
            }
            _brush.Apply();
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)_circle.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(31.5f-Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f)))));
            _circle.Apply();
        }
        void Brush(Rect r,Color color,float tilt=0)
        {
            Matrix4x4 m=GUI.matrix;GUI.matrix=m*Matrix4x4.Translate(r.center)*Matrix4x4.Rotate(Quaternion.Euler(0,0,tilt))*Matrix4x4.Translate(-r.center);GUI.color=color;GUI.DrawTexture(r,_brush);GUI.color=Color.white;GUI.matrix=m;
        }
        void Circle(Rect r,Color color){GUI.color=color;GUI.DrawTexture(r,_circle);GUI.color=Color.white;}
        void Outline(Rect r,string value,int size,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            Text(new Rect(r.x+3,r.y+3,r.width,r.height),value,size,Color.black,alignment);Text(r,value,size,color,alignment);
        }
        void GemIcon(float x,float y,float size)
        {
            var m=GUI.matrix;Vector3 pivot=new Vector3(x+size/2,y+size/2,0);GUI.matrix=m*Matrix4x4.Translate(pivot)*Matrix4x4.Rotate(Quaternion.Euler(0,0,45))*Matrix4x4.Translate(-pivot);Panel(new Rect(x,y,size,size),new Color(.35f,.05f,.5f));Panel(new Rect(x+3,y+3,size-6,size-6),_purple);GUI.matrix=m;
        }
        void Hud()
        {
            EnsureArt();
            Brush(new Rect(16,18,285,78),_ink,-2);
            _label.fontStyle=FontStyle.BoldAndItalic;
            Outline(new Rect(38,15,255,80),$"{_run.Distance:N0} m",53,Color.white);
            _label.fontStyle=FontStyle.Bold;
            Brush(new Rect(19,98,240,36),_ink,-1);
            Text(new Rect(35,97,218,35),$"BEST  {_run.BestDistance:N0} m",19,_cyan);
            for(int i=0;i<3;i++)Panel(new Rect(35+i*30,145,22,5),i<_run.Hull?_cyan:new Color(.25f,.28f,.3f));
            Brush(new Rect(1450,18,140,49),_ink);
            Circle(new Rect(1447,15,49,49),new Color(.32f,.16f,.01f));Circle(new Rect(1451,19,41,41),new Color(1,.65f,.02f));Circle(new Rect(1457,25,29,29),new Color(1,.85f,.22f));
            Text(new Rect(1453,18,38,41),"$",27,new Color(.7f,.35f,.01f),TextAnchor.MiddleCenter);
            Outline(new Rect(1498,20,75,42),_run.Coins.ToString(),30,Color.white,TextAnchor.MiddleRight);
            Brush(new Rect(1450,74,140,46),_ink);GemIcon(1455,81,30);
            Outline(new Rect(1498,74,75,42),_run.Diamonds.ToString(),30,Color.white,TextAnchor.MiddleRight);
            if(_run.Combo>1)
            {
                Brush(new Rect(1300,145,292,69),_ink,-9);
                Outline(new Rect(1313,143,258,67),$"COMBO ×{_run.Combo}",35,new Color(1,.84f,.08f),TextAnchor.MiddleRight);
            }
            bool active=_run.NitroActive,ready=_run.NitroReady;
            Brush(new Rect(1320,224,272,46),active||ready?_cyan:_ink,-6);
            Text(new Rect(1333,224,240,43),active?"NITRO!":ready?"NITRO READY!":$"NITRO  {_run.NitroCharge:0}%",22,active||ready?_ink:_cyan,TextAnchor.MiddleRight);
            Panel(new Rect(1342,275,226,5),new Color(.1f,.16f,.2f));Panel(new Rect(1342,275,226*(active?_run.NitroRemaining/3.5f:_run.NitroCharge/100),5),_cyan);
            if(_board.State.DriftAmount>.2f)Outline(new Rect(1350,295,220,42),"DRIFT +",24,_cyan,TextAnchor.MiddleRight);
            Brush(new Rect(1260,811,333,65),_ink,-8);
            Outline(new Rect(1475,738,106,73),$"{_board.State.Speed*3.6f:0}",60,Color.white,TextAnchor.MiddleRight);
            Text(new Rect(1473,810,107,26),"KM/H",20,Color.white,TextAnchor.MiddleRight);
            float speed=Mathf.Clamp01(_board.State.Speed/(_board.Tuning.data.softCapSpeed+9));
            for(int i=0;i<8;i++)Brush(new Rect(1275+i*24,829-i*2,27,23),speed>(i+.25f)/8?new Color(1,.8f,.06f):new Color(.22f,.25f,.28f));
            Crew();
            if(_run.Distance<35&&_run.Phase==RunPhase.Playing)
                Text(new Rect(410,35,760,38),"Ağırlığını kaydır. Açık yolu bul. Nitroyla hızlan!",21,_cyan,TextAnchor.MiddleCenter);
            if(!_board.State.Grounded && !_board.State.Crashed)
                Outline(new Rect(600,175,400,70),"AIR TIME!",32,new Color(1,.85f,.1f),TextAnchor.MiddleCenter);
            if(_run.Phase==RunPhase.Finishing)Outline(new Rect(400,310,800,130),"EKİP DAĞILDI!",62,Color.white,TextAnchor.MiddleCenter);
        }
        void PlayerName(Rect rect,string name,int size,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            _label.wordWrap=false;
            _label.fontSize=size;
            while(size>14 && _label.CalcSize(new GUIContent(name)).x>rect.width){size--;_label.fontSize=size;}
            Text(rect,name,size,Color.white,alignment);
            _label.wordWrap=true;
        }
        void Portrait(Rect rect,int slot,Color tint)
        {
            if(_portraits[slot]==null)return;
            GUI.color=tint;GUI.DrawTexture(rect,_portraits[slot],ScaleMode.ScaleToFit,true);GUI.color=Color.white;
        }
        void Crew()
        {
            int count=_board.Simulation.ActivePlayerCount;
            Brush(new Rect(16,799,count*64+18,84),_ink);
            int selected=GetComponent<GameManager>().InputRouter.KeyboardSlot;
            for(int i=0;i<count;i++)
            {
                float x=32+i*64;Color color=MaterialLibrary.PlayerColors[i];bool onBoard=_board.Simulation.Players[i].IsOnBoard;
                if(i==selected)Circle(new Rect(x-3,784,50,50),Color.white);
                Circle(new Rect(x,787,44,44),color);
                Portrait(new Rect(x-4,780,52,56),i,onBoard?Color.white:new Color(.4f,.4f,.4f,.6f));
                Panel(new Rect(x+6,842,32,4),color);
                GemIcon(x+17,866,11);
            }
        }
        void OnDestroy(){if(_brush!=null)Destroy(_brush);if(_circle!=null)Destroy(_circle);if(_menuShade!=null)Destroy(_menuShade);}
        void Modal(string title)
        {
            Panel(new Rect(0,0,1600,900), new Color(0, .025f,.04f,.7f));
            Panel(new Rect(500,150,600,610), _ink);
            Panel(new Rect(546,191,60,5), _cyan);
            Text(new Rect(546,218,508,90), title, 42, Color.white);
        }
        void Pause()
        {
            Modal("BİR NEFES AL");
            Button(new Rect(546,343,508,64), "DEVAM ET", true);
            Button(new Rect(546,431,508,64), "YENİDEN BAŞLA");
            Button(new Rect(546,519,508,64), _muted ? "SESİ AÇ" : "SESİ KAPAT");
            Button(new Rect(546,607,508,64), "ANA MENÜ");
        }
        void ResultChoice(Rect rect,string label,bool primary)
        {
            bool selected=primary||rect.Contains(Event.current.mousePosition);
            Brush(rect,selected?_gold:new Color(.035f,.032f,.025f,.95f),-2);
            _label.fontStyle=FontStyle.BoldAndItalic;
            Text(new Rect(rect.x+42,rect.y,rect.width-66,rect.height),label,27,selected?new Color(.04f,.035f,.025f):Color.white);
            _label.fontStyle=FontStyle.Bold;
        }
        void Results()
        {
            EnsureArt();
            _label.fontStyle=FontStyle.BoldAndItalic;
            Outline(new Rect(74,35,165,58),"kai kai",29,_gold);
            _label.fontStyle=FontStyle.Bold;
            _label.fontStyle=FontStyle.BoldAndItalic;
            Outline(new Rect(250,35,1100,70),"EKİP SIRALAMASI",51,_gold,TextAnchor.MiddleCenter);
            _label.fontStyle=FontStyle.Bold;
            Text(new Rect(250,113,1100,34),$"{_run.Distance:N0} m  •  {_run.Coins} coin  •  {_run.Diamonds} elmas  •  Ekip skoru {_run.Score:N0}",20,new Color(1,1,1,.85f),TextAnchor.MiddleCenter);
            var riders=_run.Leaderboard.Results;
            for(int rank=0;rank<Mathf.Min(3,riders.Length);rank++)
            {
                var rider=riders[rank];float x=rank==0?665:rank==1?365:965;float y=rank==0?190:rank==1?245:285;
                Color medal=rank==0?_gold:rank==1?new Color(.94f,.92f,.82f):new Color(.78f,.62f,.38f);
                Brush(new Rect(x,y+155,270,405-y),new Color(.035f,.032f,.025f,.96f));
                Panel(new Rect(x+12,y+166,246,7),medal);
                Circle(new Rect(x+89,y+8,92,92),medal);Circle(new Rect(x+96,y+15,78,78),MaterialLibrary.PlayerColors[rider.Slot]);
                Portrait(new Rect(x+81,y-3,108,108),rider.Slot,Color.white);
                PlayerName(new Rect(x+12,y+108,246,46),rider.Nick,27,TextAnchor.MiddleCenter);
                Text(new Rect(x+16,461,238,54),rider.Points.ToString("N0"),38,medal,TextAnchor.MiddleCenter);
                Text(new Rect(x+16,518,238,28),"PUAN",14,new Color(.9f,.87f,.79f),TextAnchor.MiddleCenter);
                Text(new Rect(x+16,y+178,48,39),(rank+1).ToString(),24,medal,TextAnchor.MiddleCenter);
            }
            for(int rank=3;rank<riders.Length;rank++)
            {
                var rider=riders[rank];float y=576+(rank-3)*58;
                Brush(new Rect(350,y,900,51),new Color(.035f,.032f,.025f,.94f));
                Text(new Rect(372,y,45,47),(rank+1).ToString(),24,_gold);
                Circle(new Rect(431,y+7,36,36),MaterialLibrary.PlayerColors[rider.Slot]);
                Portrait(new Rect(426,y+1,46,46),rider.Slot,Color.white);
                PlayerName(new Rect(490,y,460,47),rider.Nick,23);
                Text(new Rect(1030,y,190,47),$"{rider.Points:N0} puan",24,Color.white,TextAnchor.MiddleRight);
            }
            ResultChoice(new Rect(350,792,430,62),"TEKRAR OYNA",true);
            ResultChoice(new Rect(820,792,430,62),"ANA MENÜ",false);
        }
    }
}

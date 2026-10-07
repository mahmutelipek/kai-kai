using UnityEngine;
using UnityEngine.InputSystem;
namespace Game
{
    public sealed partial class RunHud
    {
        public enum MenuPage { Home, Crew, Settings, Credits }
        public MenuPage CurrentMenuPage => _menuPage;
        MenuPage _menuPage;
        Texture2D _menuBackground, _menuShade;
        int _menuChoice, _settingChoice;
        bool _shake=true,_speedEffects=true;
        readonly Color _gold=new Color(1,.79f,.12f);
        void InitializeMenu()
        {
            _menuBackground=Resources.Load<Texture2D>("Art/Menu/MenuBackground");
            _menuShade=new Texture2D(256,2,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
            for(int x=0;x<256;x++)for(int y=0;y<2;y++)
                _menuShade.SetPixel(x,y,new Color(.01f,.015f,.02f,Mathf.Lerp(.91f,0,Mathf.SmoothStep(0,1,x/255f))));
            _menuShade.Apply();
        }
        void DrawMenuBackground(float dim=0)
        {
            if(_menuBackground!=null) GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),_menuBackground,ScaleMode.ScaleAndCrop);
            GUI.DrawTexture(new Rect(0,0,Screen.width*.67f,Screen.height),_menuShade);
            if(dim>0){GUI.color=new Color(0,0,0,dim);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;}
        }
        void HandleMenuKeys(Keyboard kb,Gamepad pad)
        {
            if((kb!=null&&kb.escapeKey.wasPressedThisFrame)||(pad!=null&&pad.buttonEast.wasPressedThisFrame)){_menuPage=MenuPage.Home;return;}
            bool up=(kb!=null&&kb.upArrowKey.wasPressedThisFrame)||(pad!=null&&pad.dpad.up.wasPressedThisFrame);
            bool down=(kb!=null&&kb.downArrowKey.wasPressedThisFrame)||(pad!=null&&pad.dpad.down.wasPressedThisFrame);
            if(_menuPage==MenuPage.Home){if(up)_menuChoice=(_menuChoice+3)%4;if(down)_menuChoice=(_menuChoice+1)%4;}
            if(_menuPage==MenuPage.Settings){if(up)_settingChoice=(_settingChoice+2)%3;if(down)_settingChoice=(_settingChoice+1)%3;}
            if(_menuPage==MenuPage.Crew)
            {
                if((kb!=null&&kb.leftArrowKey.wasPressedThisFrame)||(pad!=null&&pad.dpad.left.wasPressedThisFrame))_players=Mathf.Max(2,_players-1);
                if((kb!=null&&kb.rightArrowKey.wasPressedThisFrame)||(pad!=null&&pad.dpad.right.wasPressedThisFrame))_players=Mathf.Min(6,_players+1);
            }
            if(pad!=null&&pad.buttonSouth.wasPressedThisFrame)ConfirmMenu();
        }
        void ConfirmMenu()
        {
            if(_menuPage==MenuPage.Home)ChooseHome(_menuChoice);
            else if(_menuPage==MenuPage.Crew)StartRun();
            else if(_menuPage==MenuPage.Settings)ToggleSetting(_settingChoice);
            else _menuPage=MenuPage.Home;
        }
        void ChooseHome(int choice)
        {
            if(choice==0)_menuPage=MenuPage.Crew;
            else if(choice==1)_menuPage=MenuPage.Settings;
            else if(choice==2)_menuPage=MenuPage.Credits;
            else Application.Quit();
        }
        void ClickMenu(Vector2 point)
        {
            if(_menuPage==MenuPage.Home)
            {
                if(new Rect(64,437,350,82).Contains(point))ChooseHome(0);
                for(int i=1;i<4;i++)if(new Rect(94,530+(i-1)*62,330,52).Contains(point))ChooseHome(i);
                return;
            }
            if(new Rect(62,752,380,62).Contains(point)){_menuPage=MenuPage.Home;return;}
            if(_menuPage==MenuPage.Crew)
            {
                for(int i=2;i<=6;i++)if(new Rect(70+(i-2)*88,450,74,58).Contains(point))_players=i;
                if(new Rect(62,548,466,74).Contains(point))StartRun();
            }
            else if(_menuPage==MenuPage.Settings)
            {
                for(int i=0;i<3;i++)if(new Rect(70,400+i*80,470,62).Contains(point))ToggleSetting(i);
            }
        }
        void ToggleSetting(int setting)
        {
            if(setting==0){_muted=!_muted;GetComponent<RunFeedback>().Muted=_muted;}
            if(setting==1){_shake=!_shake;var camera=GetComponent<GameManager>().CameraRig;camera.highSpeedShake=_shake?.06f:0;camera.impactShake=_shake?.35f:0;}
            if(setting==2){_speedEffects=!_speedEffects;GetComponentInChildren<RunVisualPolish>().SpeedLinesEnabled=_speedEffects;}
        }
        void MenuChoice(Rect r,string text,bool selected,int size=31)
        {
            bool hover=r.Contains(Event.current.mousePosition);
            if(selected||hover)Brush(new Rect(r.x-24,r.y-7,r.width+25,r.height+15),_gold,-2);
            _label.fontStyle=FontStyle.BoldAndItalic;
            Text(new Rect(r.x+22,r.y,r.width-22,r.height),text,size,selected||hover?_ink:new Color(.87f,.87f,.85f));_label.fontStyle=FontStyle.Bold;
        }
        void Menu()
        {
            EnsureArt();
            Matrix4x4 m=GUI.matrix;Vector3 pivot=new Vector3(280,240,0);
            GUI.matrix=m*Matrix4x4.Translate(pivot)*Matrix4x4.Rotate(Quaternion.Euler(0,0,-10))*Matrix4x4.Translate(-pivot);
            _label.fontStyle=FontStyle.BoldAndItalic;
            Outline(new Rect(96,88,380,145),"kai",122,Color.white);
            Outline(new Rect(93,210,450,132),"kai",122,_gold);
            _label.fontStyle=FontStyle.Bold;GUI.matrix=m;
            Text(new Rect(98,343,480,32),"DOWNHILL CREW",17,new Color(1,1,1,.7f));
            if(_menuPage==MenuPage.Home)
            {
                MenuChoice(new Rect(94,449,295,56),"PLAY",_menuChoice==0);
                MenuChoice(new Rect(94,530,295,52),"SETTINGS",_menuChoice==1);
                MenuChoice(new Rect(94,592,295,52),"CREDITS",_menuChoice==2);
                MenuChoice(new Rect(94,654,295,52),"QUIT",_menuChoice==3);
            }
            else
            {
                Brush(new Rect(48,382,530,350),new Color(.02f,.035f,.05f,.88f));
                if(_menuPage==MenuPage.Crew)
                {
                    Text(new Rect(92,390,438,44),"EKİBİNİ SEÇ",28,Color.white);
                    for(int i=2;i<=6;i++)
                    {
                        Rect r=new Rect(70+(i-2)*88,450,74,58);Brush(r,_players==i?_gold:_ink);
                        Text(r,i.ToString(),28,_players==i?_ink:Color.white,TextAnchor.MiddleCenter);
                    }
                    MenuChoice(new Rect(86,557,425,58),"YOLA ÇIK  →",true);
                    Brush(new Rect(620,255,600,410),new Color(.02f,.035f,.05f,.92f));
                    Text(new Rect(657,279,515,45),"EKİP",25,Color.white);
                    for(int slot=0;slot<_players;slot++)
                    {
                        float y=339+slot*48;
                        Portrait(new Rect(655,y,42,42),slot,Color.white);
                        PlayerName(new Rect(716,y,454,42),_run.Leaderboard.Nick(slot),22);
                    }
                    Text(new Rect(92,645,430,42),"Boş yerleri botlar doldurur.",17,new Color(.8f,.85f,.9f));
                }
                if(_menuPage==MenuPage.Settings)
                {
                    MenuChoice(new Rect(92,404,410,52),_muted?"SES: KAPALI":"SES: AÇIK",_settingChoice==0,24);
                    MenuChoice(new Rect(92,484,410,52),_shake?"KAMERA SARSINTISI: AÇIK":"KAMERA SARSINTISI: KAPALI",_settingChoice==1,22);
                    MenuChoice(new Rect(92,564,410,52),_speedEffects?"HIZ EFEKTİ: AÇIK":"HIZ EFEKTİ: KAPALI",_settingChoice==2,24);
                }
                if(_menuPage==MenuPage.Credits)
                {
                    Text(new Rect(92,403,434,48),"kai kai",34,_gold);
                    Text(new Rect(92,468,434,193),"DOWNHILL CREW\n\nBir kaykay. Bir ekip. Sonsuz iniş.\n\nKarakterler ve modeller: proje kaynakları\nMenü görseli: sağlanan referans görsel\nUnity 6 • URP",20,new Color(.85f,.88f,.9f));
                }
                MenuChoice(new Rect(86,756,340,54),"← BACK",false);
            }
        }
    }
}

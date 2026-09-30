using System.Collections.Generic;

namespace Game.Frontend
{
    public enum Language : byte { English = 0, Turkish = 1 }

    /// <summary>
    /// UI text in English and Turkish. The English text is the key, so untranslated strings still read fine.
    /// Lookups are dictionary reads (no allocation). Add a language: add an enum value and a table.
    /// </summary>
    public static class Loc
    {
        public static Language Current = Language.English;

        public static readonly string[] LanguageNames = { "ENGLISH", "TÜRKÇE" };

        public static string T(string english)
        {
            if (Current == Language.English || english == null) return english;
            if (Turkish.TryGetValue(english, out string t)) return t;
            if (TrackMissing) Missing.Add(english);
            return english;
        }

        /// <summary>Tests: record keys asked for that the current language lacks.</summary>
        public static bool TrackMissing;
        public static readonly HashSet<string> Missing = new HashSet<string>();

        public static bool Has(Language lang, string english) => lang == Language.English || Turkish.ContainsKey(english);

        /// <summary>Every key the game uses (tests check each language covers them).</summary>
        public static IEnumerable<string> Keys => Turkish.Keys;

        static readonly Dictionary<string, string> Turkish = new Dictionary<string, string>
        {
            // title / menus
            ["DOWNHILL PARTY BOARD"] = "DOWNHILL PARTY BOARD",
            ["PLAY"] = "OYNA",
            ["SETTINGS"] = "AYARLAR",
            ["QUIT"] = "ÇIKIŞ",
            ["PRESS (A) OR SPACE"] = "(A) YA DA BOŞLUK TUŞUNA BAS",
            ["WHO'S RIDING?"] = "KİMLER BİNİYOR?",
            ["PRESS (A) / SPACE TO JOIN"] = "KATILMAK İÇİN (A) / BOŞLUK",
            ["ENTER: SECOND KEYBOARD RIDER (ARROWS)"] = "ENTER: İKİNCİ KLAVYE OYUNCUSU (OKLAR)",
            ["(B) / ESC: LEAVE"] = "(B) / ESC: AYRIL",
            ["JOIN"] = "KATIL",
            ["KEYBOARD"] = "KLAVYE",
            ["KEYBOARD (ARROWS)"] = "KLAVYE (OKLAR)",
            ["GAMEPAD"] = "GAMEPAD",
            ["BOT"] = "BOT",
            ["CREW SIZE"] = "EKİP",
            ["BOTS FILL EMPTY SPOTS"] = "BOŞ YERLERE BOT",
            ["START"] = "BAŞLAT",
            ["BACK"] = "GERİ",
            ["ON"] = "AÇIK",
            ["NATIVE"] = "DOĞAL",
            ["OFF"] = "KAPALI",
            ["PAUSED"] = "DURAKLATILDI",
            ["RESUME"] = "DEVAM",
            ["RESTART"] = "YENİDEN BAŞLA",
            ["MAIN MENU"] = "ANA MENÜ",
            ["DISPLAY MODE"] = "EKRAN MODU",
            ["FULLSCREEN"] = "TAM EKRAN",
            ["BORDERLESS"] = "ÇERÇEVESİZ",
            ["WINDOWED"] = "PENCERE",
            ["RESOLUTION"] = "ÇÖZÜNÜRLÜK",
            ["VSYNC"] = "DİKEY SENKRON",
            ["QUALITY"] = "KALİTE",
            ["LOW"] = "DÜŞÜK",
            ["MEDIUM"] = "ORTA",
            ["HIGH"] = "YÜKSEK",
            ["REDUCE MOTION"] = "HAREKETİ AZALT",
            ["LANGUAGE"] = "DİL",
            ["MASTER VOLUME"] = "ANA SES",
            ["MUSIC VOLUME"] = "MÜZİK SESİ",
            ["EFFECTS VOLUME"] = "EFEKT SESİ",
            ["MOVE: STICK / WASD   JUMP: (A) / SPACE   NITRO: (X) / E"] = "HAREKET: ÇUBUK / WASD   ZIPLA: (A) / BOŞLUK   NİTRO: (X) / E",
            ["STAND WHERE YOU WANT THE BOARD TO GO!"] = "TAHTAYI NEREYE İSTİYORSAN ORAYA GEÇ!",
            // HUD
            ["BEST"] = "EN İYİ",
            ["COMBO"] = "KOMBO",
            ["NITRO READY!"] = "NİTRO HAZIR!",
            ["NITRO!"] = "NİTRO!",
            ["KM/H"] = "KM/S",
            ["GO!"] = "BAŞLA!",
            ["NEAR MISS!"] = "KIL PAYI!",
            ["CLEAN LANDING!"] = "TEMİZ İNİŞ!",
            ["S AIR"] = " SN HAVADA",
            ["SECTION CLEARED!"] = "BÖLÜM GEÇİLDİ!",
            ["DIAMOND!"] = "ELMAS!",
            ["NITRO GET!"] = "NİTRO ALINDI!",
            ["NITRO BOOST!"] = "NİTRO GAZI!",
            ["OUCH! COMBO LOST"] = "AH! KOMBO GİTTİ",
            ["WIPEOUT!"] = "DÜŞTÜNÜZ!",
            ["LEFT"] = "KALDI",
            ["HOLD ON! BALANCE!"] = "TUTUNUN! DENGE!",
            ["RIDERS: "] = "SÜRÜCÜ: ",
            ["REDUCED MOTION ON"] = "HAREKET AZALTILDI",
            ["REDUCED MOTION OFF"] = "HAREKET NORMAL",
            // end screen
            ["RUN OVER"] = "KOŞU BİTTİ",
            ["DISTANCE"] = "MESAFE",
            ["COINS"] = "ALTIN",
            ["DIAMONDS"] = "ELMAS",
            ["NEAR MISSES"] = "KIL PAYI",
            ["AIRTIME"] = "HAVADA",
            ["BEST COMBO"] = "EN İYİ KOMBO",
            ["SCORE"] = "SKOR",
            ["NEW BEST SCORE!"] = "YENİ REKOR!",
            ["NEW BEST DISTANCE!"] = "YENİ MESAFE REKORU!",
            ["PRESS R, SPACE OR (A) TO RIDE AGAIN"] = "TEKRAR İÇİN R, BOŞLUK YA DA (A)",
            ["ACHIEVEMENT UNLOCKED"] = "BAŞARIM AÇILDI",
            ["OLLIE!"] = "OLLIE!",
            ["PERFECT OLLIE!"] = "MÜKEMMEL OLLIE!",
            ["CARVE BOOST!"] = "VİRAJ TURBOSU!",
            ["MEGA CARVE BOOST!"] = "MEGA VİRAJ TURBOSU!",
            ["SLIPSTREAM!"] = "RÜZGAR TÜNELİ!",
            ["TRICKS"] = "HAREKETLER",
            // M4.4: menus, tips, move meter
            ["HOW TO PLAY"] = "NASIL OYNANIR",
            ["CREDITS"] = "EMEĞİ GEÇENLER",
            ["RIDING TIPS"] = "SÜRÜŞ İPUÇLARI",
            ["STEER BY STANDING"] = "DURARAK YÖNLENDİR",
            ["EVERYONE RIDES ONE GIANT BOARD."] = "HERKES TEK BİR DEV KAYKAYDA.",
            ["WHERE THE CREW STANDS, IT GOES."] = "EKİP NEREDE DURURSA KAYKAY ORAYA GİDER.",
            ["CREW OLLIE"] = "EKİP OLLIE",
            ["JUMP TOGETHER TO HOP POTHOLES AND CONES."] = "ÇUKUR VE KONİLERİN ÜSTÜNDEN BİRLİKTE ZIPLA.",
            ["ALL AT ONCE = PERFECT OLLIE!"] = "HEP BİRLİKTE = PERFECT OLLIE!",
            ["CARVE BOOST"] = "VİRAJ FIRLATMASI",
            ["HOLD A HARD CARVE, THEN STRAIGHTEN OUT"] = "SERT BİR VİRAJI TUT, SONRA DÜZEL",
            ["CLEANLY FOR A BURST OF SPEED."] = "VE ANİ BİR HIZ KAZAN.",
            ["DRAFT"] = "RÜZGÂR TÜNELİ",
            ["RIDE RIGHT BEHIND A CAR GOING YOUR WAY"] = "SENİNLE AYNI YÖNE GİDEN ARABANIN ARKASINA GİR",
            ["AND GET PULLED ALONG FASTER."] = "VE DAHA HIZLI SÜRÜKLEN.",
            ["NITRO"] = "NİTRO",
            ["GRAB THE LIGHTNING, THEN PRESS"] = "ŞİMŞEĞİ AL, SONRA BAS:",
            ["ACTION: (X) / E TO FIRE IT."] = "AKSİYON: (X) / E İLE ATEŞLE.",
            ["COMBO"] = "COMBO",
            ["COINS, NEAR MISSES AND TRICKS BUILD IT."] = "COIN, KIL PAYI VE HAREKETLER ARTIRIR.",
            ["CRASH OR TUMBLE AND IT'S GONE!"] = "DÜŞERSEN HEPSİ GİDER!",
            ["A GAME BY"] = "YAPIM",
            ["FONT"] = "YAZI TİPİ",
            ["ENGINE"] = "MOTOR",
            ["SOUND AND MUSIC"] = "SES VE MÜZİK",
            ["PLAYTESTERS"] = "TEST EDENLER",
            ["LEAN LEFT OR RIGHT: WHERE YOU STAND STEERS THE BOARD!"] = "SOLA YA DA SAĞA YASLAN: DURDUĞUN YER KAYKAYI YÖNLENDİRİR!",
            ["EVERYONE JUMP TOGETHER TO OLLIE OVER IT!"] = "ÜSTÜNDEN ATLAMAK İÇİN HEP BİRLİKTE ZIPLAYIN!",
            ["HOLD THE CARVE... THEN STRAIGHTEN OUT FOR A BOOST!"] = "VİRAJI TUT... SONRA DÜZEL VE HIZLAN!",
            ["RIDE RIGHT BEHIND CARS TO DRAFT FOR SPEED!"] = "HIZ İÇİN ARABALARIN HEMEN ARKASINA GİR!",
            ["NITRO READY! PRESS ACTION: (X) / E"] = "NİTRO HAZIR! AKSİYON: (X) / E",
            ["BOOST!"] = "HIZ!",
            ["MEGA! STRAIGHTEN!"] = "MEGA! DÜZEL!",
            ["STRAIGHTEN!"] = "DÜZEL!",
            ["CARVE"] = "VİRAJ",
            ["DRAFTING"] = "RÜZGÂR TÜNELİ",
            ["JUMP!"] = "ZIPLA!",
            ["NEW BEST!"] = "YENİ REKOR!",
            ["JUMP TOGETHER TO OLLIE!"] = "BİRLİKTE ZIPLA, TAHTA SIÇRASIN!",
        };
    }
}

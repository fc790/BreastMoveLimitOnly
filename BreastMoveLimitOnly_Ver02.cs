using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

[BepInPlugin(GUID, NAME, VER)]
public class BreastMoveLimitOnly_Ver02 : BaseUnityPlugin
{
    public const string GUID = "fc790s.breast_movelimit_only_ver02";
    public const string NAME = "Bust/Hip MoveLimit Only (DynamicBone_Ver02)";
    public const string VER  = "1.2.2"; // bumped

    private static BreastMoveLimitOnly_Ver02 _inst;

    // ===== bust default (fallback / also used by UpdateParticles1 bust roots) =====
    private LimitCfg _bust01;
    private LimitCfg _bust02;
    private LimitCfg _bust03;

    // ===== bust left/right =====
    private LimitCfg _bustL01;
    private LimitCfg _bustL02;
    private LimitCfg _bustL03;

    private LimitCfg _bustR01;
    private LimitCfg _bustR02;
    private LimitCfg _bustR03;

    // ===== hip default =====
    private LimitCfg _hip01;
    private LimitCfg _hip02;
    private LimitCfg _hip03;

    // ===== hip left/right =====
    private LimitCfg _hipL01;
    private LimitCfg _hipL02;
    private LimitCfg _hipL03;

    private LimitCfg _hipR01;
    private LimitCfg _hipR02;
    private LimitCfg _hipR03;

    // detection / debug config
    private ConfigEntry<string> _bustTokens;
    private ConfigEntry<string> _hipTokens;
    private ConfigEntry<bool> _debugDumpVer02RootsOnce;
    private ConfigEntry<bool> _forceHipAll; // test switch

    // side detection (hip only; bust already has dbL/dbR)
    private ConfigEntry<bool> _enableHipSideDetect;
    private ConfigEntry<string> _leftTokens;
    private ConfigEntry<string> _rightTokens;
    private ConfigEntry<bool> _useLocalXAsSideFallback;

    private bool _dumpedVer02List = false;

    // Harmony Patch method (reflected) to avoid obsolete signature compile error
    private static MethodInfo _miHarmonyPatch5;

    private void Awake()
    {
        _inst = this;

        // ---- bust default ----
        _bust01 = new LimitCfg(Config, "Bust01");
        _bust02 = new LimitCfg(Config, "Bust02");
        _bust03 = new LimitCfg(Config, "Bust03");

        // ---- bust L/R ----
        _bustL01 = new LimitCfg(Config, "Bust_L01");
        _bustL02 = new LimitCfg(Config, "Bust_L02");
        _bustL03 = new LimitCfg(Config, "Bust_L03");

        _bustR01 = new LimitCfg(Config, "Bust_R01");
        _bustR02 = new LimitCfg(Config, "Bust_R02");
        _bustR03 = new LimitCfg(Config, "Bust_R03");

        // ---- hip default ----
        _hip01 = new LimitCfg(Config, "Hip01");
        _hip02 = new LimitCfg(Config, "Hip02");
        _hip03 = new LimitCfg(Config, "Hip03");

        // ---- hip L/R ----
        _hipL01 = new LimitCfg(Config, "Hip_L01");
        _hipL02 = new LimitCfg(Config, "Hip_L02");
        _hipL03 = new LimitCfg(Config, "Hip_L03");

        _hipR01 = new LimitCfg(Config, "Hip_R01");
        _hipR02 = new LimitCfg(Config, "Hip_R02");
        _hipR03 = new LimitCfg(Config, "Hip_R03");

        // Tokens are '|' separated (case-insensitive)
        _bustTokens = Config.Bind("Detect", "BustTokens",
            "bust|breast|mune|oppai|titi",
            "Root.name contains any token => treated as bust (Ver02). Token separator: |");

        // Add Japanese common words for butt/hips:
        // siri(尻), oshiri(お尻), ketsu(ケツ), koshi(腰), pelvis
        _hipTokens = Config.Bind("Detect", "HipTokens",
            "hip|hips|butt|ass|waist|pelvis|koshi|siri|oshiri|ketsu",
            "Root.name contains any token => treated as hip/butt (Ver02). Token separator: |");

        _debugDumpVer02RootsOnce = Config.Bind("Debug", "DumpVer02RootsOnce", true,
            "Dump all DynamicBone_Ver02 roots once after first UpdateParticles1 call.");

        _forceHipAll = Config.Bind("Debug", "ForceHipAll", false,
            "TEST ONLY: Treat all non-bust Ver02 as hip. Use to verify our MoveLimit write-back works.");

        // hip side detect
        _enableHipSideDetect = Config.Bind("Detect", "EnableHipSideDetect", true,
            "Try detect left/right for hip roots. If failed, fallback to Hip01~03.");

        _leftTokens = Config.Bind("Detect", "LeftTokens",
            "_l|l_|left|hidari|migi?no", // (leave simple; user can edit)
            "Root.name/path contains any token => treated as LEFT. Token separator: |");

        _rightTokens = Config.Bind("Detect", "RightTokens",
            "_r|r_|right|migi",
            "Root.name/path contains any token => treated as RIGHT. Token separator: |");

        _useLocalXAsSideFallback = Config.Bind("Detect", "UseLocalXAsSideFallback", true,
            "If name/path cannot decide side, use Root.localPosition.x (<0 left, >0 right).");

        ResolveHarmonyPatchMethod();

        TryPatchBustSoft();          // bust init on ptn change
        TryPatchDynamicBoneVer02();  // force write-back each frame
    }

    // ---------------- Harmony patch helper (avoid CS0619 obsolete error=true) ----------------

    private void ResolveHarmonyPatchMethod()
    {
        try
        {
            MethodInfo[] ms = typeof(Harmony).GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < ms.Length; i++)
            {
                MethodInfo m = ms[i];
                if (m.Name != "Patch") continue;

                ParameterInfo[] ps = m.GetParameters();
                // Patch(MethodBase, HarmonyMethod, HarmonyMethod, HarmonyMethod, HarmonyMethod)
                if (ps == null || ps.Length != 5) continue;
                if (ps[0].ParameterType != typeof(MethodBase)) continue;
                if (ps[1].ParameterType != typeof(HarmonyMethod)) continue;
                if (ps[2].ParameterType != typeof(HarmonyMethod)) continue;
                if (ps[3].ParameterType != typeof(HarmonyMethod)) continue;
                if (ps[4].ParameterType != typeof(HarmonyMethod)) continue;

                _miHarmonyPatch5 = m;
                return;
            }

            Logger.LogWarning("Harmony.Patch(MethodBase, HarmonyMethod x4) not found. Harmony version mismatch?");
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }
    }

    private void PatchPostfix(Harmony h, MethodInfo target, MethodInfo postfix, int priority)
    {
        if (h == null || target == null || postfix == null) return;
        if (_miHarmonyPatch5 == null) return;

        HarmonyMethod hmPost = new HarmonyMethod(postfix);
        hmPost.priority = priority;

        // prefix=null, postfix=hmPost, transpiler=null, finalizer=null
        _miHarmonyPatch5.Invoke(h, new object[] { target, null, hmPost, null, null });
    }

    // ---------------- Patch 1: BustSoft.ReCalc (postfix) ----------------

    private void TryPatchBustSoft()
    {
        try
        {
            Harmony h = new Harmony(GUID);

            Type tBustSoft = Type.GetType("BustSoft, Assembly-CSharp");
            if (tBustSoft == null)
            {
                Logger.LogWarning("BustSoft type not found (BustSoft, Assembly-CSharp). Skip BustSoft patch.");
                return;
            }

            MethodInfo miReCalc = tBustSoft.GetMethod("ReCalc", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (miReCalc == null)
            {
                Logger.LogWarning("BustSoft.ReCalc not found.");
                return;
            }

            MethodInfo miPost = typeof(BreastMoveLimitOnly_Ver02).GetMethod(
                "BustSoft_ReCalc_Postfix",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            if (miPost == null)
            {
                Logger.LogWarning("Postfix not found: BustSoft_ReCalc_Postfix");
                return;
            }

            PatchPostfix(h, miReCalc, miPost, Priority.Last);

            Logger.LogInfo("Patched BustSoft.ReCalc (postfix).");
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }
    }

    private static void BustSoft_ReCalc_Postfix(ref ChaControl ___chaCtrl, ref ChaInfo ___info, params int[] changePtn)
    {
        if (_inst == null) return;
        try
        {
            _inst.ApplyToBustByChangePtn(___chaCtrl, changePtn);
        }
        catch (Exception e)
        {
            _inst.Logger.LogError(e);
        }
    }

    private void ApplyToBustByChangePtn(ChaControl chaCtrl, int[] changePtn)
    {
        if (chaCtrl == null) return;
        if (changePtn == null || changePtn.Length == 0) return;

        // NOTE:
        // - For BustSoft path, we already have dbL/dbR, so we apply Bust_Lxx / Bust_Rxx preferentially.
        // - If Bust_Lxx not enabled, fallback to Bustxx (default). Same for R.

        // Read default once (fallback)
        Vector3 dmin1, dmax1, dmin2, dmax2, dmin3, dmax3;
        bool don1 = _bust01.TryGet(out dmin1, out dmax1);
        bool don2 = _bust02.TryGet(out dmin2, out dmax2);
        bool don3 = _bust03.TryGet(out dmin3, out dmax3);

        // Left
        Vector3 lmin1, lmax1, lmin2, lmax2, lmin3, lmax3;
        bool lon1 = _bustL01.TryGet(out lmin1, out lmax1);
        bool lon2 = _bustL02.TryGet(out lmin2, out lmax2);
        bool lon3 = _bustL03.TryGet(out lmin3, out lmax3);

        // Right
        Vector3 rmin1, rmax1, rmin2, rmax2, rmin3, rmax3;
        bool ron1 = _bustR01.TryGet(out rmin1, out rmax1);
        bool ron2 = _bustR02.TryGet(out rmin2, out rmax2);
        bool ron3 = _bustR03.TryGet(out rmin3, out rmax3);

        // If neither side nor default is enabled, nothing to do.
        if (!(don1 || don2 || don3 || lon1 || lon2 || lon3 || ron1 || ron2 || ron3)) return;

        // numeric enum cast (compatible with your build)
        DynamicBone_Ver02 dbL = null;
        DynamicBone_Ver02 dbR = null;
        try { dbL = chaCtrl.getDynamicBoneBust((ChaInfo.DynamicBoneKind)0); } catch { dbL = null; }
        try { dbR = chaCtrl.getDynamicBoneBust((ChaInfo.DynamicBoneKind)1); } catch { dbR = null; }

        // apply per pattern
        for (int i = 0; i < changePtn.Length; i++)
        {
            int ptn = changePtn[i];

            // Left bust
            if (dbL != null)
            {
                if (lon1) { dbL.SetMoveLimitParams(ptn, 0, true, true); dbL.SetMoveLimitData(ptn, 0, lmin1, lmax1, true); }
                else if (don1) { dbL.SetMoveLimitParams(ptn, 0, true, true); dbL.SetMoveLimitData(ptn, 0, dmin1, dmax1, true); }

                if (lon2) { dbL.SetMoveLimitParams(ptn, 1, true, true); dbL.SetMoveLimitData(ptn, 1, lmin2, lmax2, true); }
                else if (don2) { dbL.SetMoveLimitParams(ptn, 1, true, true); dbL.SetMoveLimitData(ptn, 1, dmin2, dmax2, true); }

                if (lon3) { dbL.SetMoveLimitParams(ptn, 2, true, true); dbL.SetMoveLimitData(ptn, 2, lmin3, lmax3, true); }
                else if (don3) { dbL.SetMoveLimitParams(ptn, 2, true, true); dbL.SetMoveLimitData(ptn, 2, dmin3, dmax3, true); }
            }

            // Right bust
            if (dbR != null)
            {
                if (ron1) { dbR.SetMoveLimitParams(ptn, 0, true, true); dbR.SetMoveLimitData(ptn, 0, rmin1, rmax1, true); }
                else if (don1) { dbR.SetMoveLimitParams(ptn, 0, true, true); dbR.SetMoveLimitData(ptn, 0, dmin1, dmax1, true); }

                if (ron2) { dbR.SetMoveLimitParams(ptn, 1, true, true); dbR.SetMoveLimitData(ptn, 1, rmin2, rmax2, true); }
                else if (don2) { dbR.SetMoveLimitParams(ptn, 1, true, true); dbR.SetMoveLimitData(ptn, 1, dmin2, dmax2, true); }

                if (ron3) { dbR.SetMoveLimitParams(ptn, 2, true, true); dbR.SetMoveLimitData(ptn, 2, rmin3, rmax3, true); }
                else if (don3) { dbR.SetMoveLimitParams(ptn, 2, true, true); dbR.SetMoveLimitData(ptn, 2, dmin3, dmax3, true); }
            }
        }
    }

    // ---------------- Patch 2: DynamicBone_Ver02.UpdateParticles1 (postfix) ----------------

    private void TryPatchDynamicBoneVer02()
    {
        try
        {
            Harmony h = new Harmony(GUID);

            Type tDB = Type.GetType("DynamicBone_Ver02, Assembly-CSharp");
            if (tDB == null)
            {
                Logger.LogWarning("DynamicBone_Ver02 not found (DynamicBone_Ver02, Assembly-CSharp).");
                return;
            }

            MethodInfo mi = tDB.GetMethod("UpdateParticles1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null)
            {
                Logger.LogWarning("DynamicBone_Ver02.UpdateParticles1 not found.");
                return;
            }

            MethodInfo miPost = typeof(BreastMoveLimitOnly_Ver02).GetMethod(
                "DBV02_UpdateParticles1_Postfix",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            if (miPost == null)
            {
                Logger.LogWarning("Postfix not found: DBV02_UpdateParticles1_Postfix");
                return;
            }

            PatchPostfix(h, mi, miPost, Priority.Last);

            Logger.LogInfo("Patched DynamicBone_Ver02.UpdateParticles1 (postfix).");
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }
    }

    private static void DBV02_UpdateParticles1_Postfix(object __instance)
    {
        if (_inst == null) return;
        try
        {
            _inst.ApplyMoveLimitEveryFrame(__instance);
        }
        catch (Exception e)
        {
            _inst.Logger.LogError(e);
        }
    }

    private void ApplyMoveLimitEveryFrame(object dbObj)
    {
        if (dbObj == null) return;

        DynamicBone_Ver02 db = dbObj as DynamicBone_Ver02;
        if (db == null) return;

        // dump once: list all Ver02 roots + path for you to identify butt/hip root name
        if (!_dumpedVer02List && _debugDumpVer02RootsOnce.Value)
        {
            _dumpedVer02List = true;
            DumpAllVer02RootsOnce();
        }

        Transform rt = null;
        try { rt = db.Root; } catch { rt = null; }

        string rn = "";
        try { if (rt != null) rn = rt.name; } catch { rn = ""; }
        if (rn == null) rn = "";

        string low = rn.ToLowerInvariant();

        bool isBust = MatchTokens(low, _bustTokens.Value);
        bool isHip  = MatchTokens(low, _hipTokens.Value);

        // test-only: treat all non-bust as hip
        if (!isBust && _forceHipAll.Value) isHip = true;

        if (!isBust && !isHip) return;

        int side = 0; // 0 unknown/center, -1 left, +1 right

        // For hip only: attempt detect side to use Hip_Lxx / Hip_Rxx
        if (isHip && _enableHipSideDetect.Value)
        {
            string pathLow = "";
            try { pathLow = BuildPath(rt, 12).ToLowerInvariant(); } catch { pathLow = ""; }

            side = DetectSide(pathLow, rt);
        }

        // select cfg group
        Vector3 min1, max1, min2, max2, min3, max3;
        bool on1, on2, on3;

        if (isBust)
        {
            // For Ver02 bust roots:
            // - If it is actually left/right separated root, you can also use Bust_Lxx / Bust_Rxx.
            // - Otherwise fallback to Bustxx.
            if (side < 0)
            {
                on1 = _bustL01.TryGet(out min1, out max1); if (!on1) on1 = _bust01.TryGet(out min1, out max1);
                on2 = _bustL02.TryGet(out min2, out max2); if (!on2) on2 = _bust02.TryGet(out min2, out max2);
                on3 = _bustL03.TryGet(out min3, out max3); if (!on3) on3 = _bust03.TryGet(out min3, out max3);
            }
            else if (side > 0)
            {
                on1 = _bustR01.TryGet(out min1, out max1); if (!on1) on1 = _bust01.TryGet(out min1, out max1);
                on2 = _bustR02.TryGet(out min2, out max2); if (!on2) on2 = _bust02.TryGet(out min2, out max2);
                on3 = _bustR03.TryGet(out min3, out max3); if (!on3) on3 = _bust03.TryGet(out min3, out max3);
            }
            else
            {
                on1 = _bust01.TryGet(out min1, out max1);
                on2 = _bust02.TryGet(out min2, out max2);
                on3 = _bust03.TryGet(out min3, out max3);
            }
        }
        else
        {
            if (side < 0)
            {
                on1 = _hipL01.TryGet(out min1, out max1); if (!on1) on1 = _hip01.TryGet(out min1, out max1);
                on2 = _hipL02.TryGet(out min2, out max2); if (!on2) on2 = _hip02.TryGet(out min2, out max2);
                on3 = _hipL03.TryGet(out min3, out max3); if (!on3) on3 = _hip03.TryGet(out min3, out max3);
            }
            else if (side > 0)
            {
                on1 = _hipR01.TryGet(out min1, out max1); if (!on1) on1 = _hip01.TryGet(out min1, out max1);
                on2 = _hipR02.TryGet(out min2, out max2); if (!on2) on2 = _hip02.TryGet(out min2, out max2);
                on3 = _hipR03.TryGet(out min3, out max3); if (!on3) on3 = _hip03.TryGet(out min3, out max3);
            }
            else
            {
                on1 = _hip01.TryGet(out min1, out max1);
                on2 = _hip02.TryGet(out min2, out max2);
                on3 = _hip03.TryGet(out min3, out max3);
            }
        }

        if (!on1 && !on2 && !on3) return;

        int pCount = 0;
        try { if (db.Patterns != null) pCount = db.Patterns.Count; } catch { pCount = 0; }
        if (pCount <= 0) return;

        for (int ptn = 0; ptn < pCount; ptn++)
        {
            if (on1) { db.SetMoveLimitParams(ptn, 0, true, true); db.SetMoveLimitData(ptn, 0, min1, max1, true); }
            if (on2) { db.SetMoveLimitParams(ptn, 1, true, true); db.SetMoveLimitData(ptn, 1, min2, max2, true); }
            if (on3) { db.SetMoveLimitParams(ptn, 2, true, true); db.SetMoveLimitData(ptn, 2, min3, max3, true); }
        }
    }

    // side: -1 left, +1 right, 0 unknown
    private int DetectSide(string pathLower, Transform root)
    {
        int side = 0;

        try
        {
            if (!string.IsNullOrEmpty(pathLower))
            {
                if (MatchTokens(pathLower, _leftTokens.Value)) side = -1;
                else if (MatchTokens(pathLower, _rightTokens.Value)) side = 1;
            }

            if (side == 0 && root != null)
            {
                string n = "";
                try { n = root.name; } catch { n = ""; }
                if (n == null) n = "";
                string nl = n.ToLowerInvariant();

                if (MatchTokens(nl, _leftTokens.Value)) side = -1;
                else if (MatchTokens(nl, _rightTokens.Value)) side = 1;
            }

            if (side == 0 && _useLocalXAsSideFallback.Value && root != null)
            {
                float x = 0f;
                try { x = root.localPosition.x; } catch { x = 0f; }
                if (x < -0.0001f) side = -1;
                else if (x > 0.0001f) side = 1;
            }
        }
        catch { side = 0; }

        return side;
    }

    private void DumpAllVer02RootsOnce()
    {
        try
        {
            Type tDB = Type.GetType("DynamicBone_Ver02, Assembly-CSharp");
            if (tDB == null) { Logger.LogWarning("DBG: DynamicBone_Ver02 type missing."); return; }

            UnityEngine.Object[] arr = null;
            try { arr = UnityEngine.Object.FindObjectsOfType(tDB); } catch { arr = null; }
            if (arr == null || arr.Length == 0)
            {
                Logger.LogWarning("DBG: no DynamicBone_Ver02 objects found.");
                return;
            }

            Logger.LogInfo("DBG: DynamicBone_Ver02 list count=" + arr.Length);

            int printed = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                DynamicBone_Ver02 db = arr[i] as DynamicBone_Ver02;
                if (db == null) continue;

                string rootName = "";
                Transform rt = null;
                try { rt = db.Root; } catch { rt = null; }
                if (rt != null) rootName = rt.name;

                string path = BuildPath(rt, 10);

                Logger.LogInfo("DBG Ver02 root=" + rootName + " path=" + path);
                printed++;
                if (printed >= 80) break; // avoid huge spam
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }
    }

    private static string BuildPath(Transform t, int maxUp)
    {
        if (t == null) return "";
        string p = t.name;
        int up = 0;
        Transform cur = t;
        while (cur != null && cur.parent != null && up < maxUp)
        {
            cur = cur.parent;
            p = cur.name + "/" + p;
            up++;
        }
        return p;
    }

    private static bool MatchTokens(string lowerName, string tokens)
    {
        if (string.IsNullOrEmpty(lowerName) || string.IsNullOrEmpty(tokens)) return false;

        string[] parts = tokens.Split('|');
        for (int i = 0; i < parts.Length; i++)
        {
            string tk = parts[i];
            if (tk == null) continue;
            tk = tk.Trim().ToLowerInvariant();
            if (tk.Length == 0) continue;

            if (lowerName.IndexOf(tk) >= 0) return true;
        }
        return false;
    }

    // ---------------- cfg ----------------
    private class LimitCfg
    {
        public ConfigEntry<bool> On;
        public ConfigEntry<string> MinStr;
        public ConfigEntry<string> MaxStr;

        public LimitCfg(ConfigFile cfg, string section)
        {
            On = cfg.Bind(section, "MoveLimitOn", false, "Enable MoveLimit clamp for this group (idx 0/1/2).");
            MinStr = cfg.Bind(section, "MoveLimitMin", "0,0,0", "Min offset (Vector3) x,y,z");
            MaxStr = cfg.Bind(section, "MoveLimitMax", "0,0,0", "Max offset (Vector3) x,y,z");
        }

        public bool TryGet(out Vector3 min, out Vector3 max)
        {
            min = Vector3.zero;
            max = Vector3.zero;

            if (!On.Value) return false;
            if (!TryParseVec3(MinStr.Value, out min)) return false;
            if (!TryParseVec3(MaxStr.Value, out max)) return false;
            return true;
        }

        private static bool TryParseVec3(string s, out Vector3 v)
        {
            v = Vector3.zero;
            if (s == null) return false;

            s = s.Trim();
            string[] parts = s.Split(',');
            if (parts == null || parts.Length != 3) return false;

            float fx, fy, fz;
            if (!float.TryParse(parts[0].Trim(), out fx)) return false;
            if (!float.TryParse(parts[1].Trim(), out fy)) return false;
            if (!float.TryParse(parts[2].Trim(), out fz)) return false;

            v = new Vector3(fx, fy, fz);
            return true;
        }
    }
}
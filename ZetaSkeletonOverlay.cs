using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GameOverlay.Drawing;
using GameOverlay.Windows;

public class ZetaSkeletonOverlay : IDisposable
{
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    // ── Değişkenler ──
    private readonly GraphicsWindow _window;
    private readonly ZetaMemory _mem;
    private readonly WorldToScreen _w2s;

    private SolidBrush? _greenBrush;
    private SolidBrush? _yellowBrush;
    private SolidBrush? _redBrush;
    private SolidBrush? _whiteBrush;
    private SolidBrush? _bgBrush;
    private Font? _font;
    private Font? _fontSmall;

    public bool IsEspActive = true;
    public int ToggleKey = 0x74;
    private bool _isKeyPressed = false;
    private int _entityFoundCount = 0;

    // ════════════════════════════════════════════════
    //  FiveM b3095 OFFSET'LERİ
    // ════════════════════════════════════════════════

    // World pointer: BaseAddress + bu offset → CWorld*
    private const long WORLD_OFFSET = 0x252D738;

    // CWorld yapısı içindeki alanlar
    private const long LOCAL_PLAYER_OFFSET = 0x8;      // CWorld + 0x8 → yerel oyuncu CPed*
    private const long REPLAY_INTERFACE_OFFSET = 0x18;  // CWorld + 0x18 → CReplayInterface*

    // CReplayInterface → CPedList
    private const long PED_INTERFACE_OFFSET = 0x18;     // CReplayInterface + 0x18 → CPedInterface*
    private const long PED_LIST_OFFSET = 0x100;         // CPedInterface + 0x100 → CPedList*
    private const long PED_MAX_OFFSET = 0x108;          // CPedInterface + 0x108 → max ped sayısı (int)

    // CPed yapısı içindeki alanlar (b3095)
    private const long HEALTH_OFFSET = 0x280;           // CPed + 0x280 → float health
    private const long HEALTH_MAX_OFFSET = 0x2A0;       // CPed + 0x2A0 → float max health
    private const long PED_TYPE_OFFSET = 0x10B8;        // CPed + 0x10B8 → ped tipi
    private const long POSITION_OFFSET = 0x90;          // CPed + 0x90 → CNavigation* (konum)
    private const long SKELETON_OFFSET = 0x430;         // CPed + 0x430 → CSkeletonData*

    // CSkeletonData yapısı
    private const long BONE_CACHE_OFFSET = 0x18;        // CSkeletonData + 0x18 → bone position cache
    private const long BONE_COUNT_OFFSET = 0x20;        // CSkeletonData + 0x20 → ushort bone sayısı

    // ── Her bone 0x20 (32 byte) genişliğinde ──
    // Bone yapısı: [4x4 rotation(16 byte)] + [x(4) y(4) z(4)] + [pad(4)]
    // Pozisyon: boneBase + (boneIndex * 0x20) + 0x10 → x, +0x14 → y, +0x18 → z
    private const int BONE_STRIDE = 0x20;
    private const int BONE_POS_X = 0x10;
    private const int BONE_POS_Y = 0x14;
    private const int BONE_POS_Z = 0x18;

    // ── İskelet için Kullanılan Bone ID'leri ──
    // GTA V / FiveM standart ped bone indeksleri
    private static readonly int[] BoneIndices =
    {
        // 0: SKEL_Pelvis (kalça merkezi)
        0,
        // 1: SKEL_Spine2 (göğüs)
        5,
        // 2: SKEL_Spine3 (üst göğüs)
        6,
        // 3: SKEL_Neck_1 (boyun)
        7,
        // 4: SKEL_Head (kafa)
        8,
        // 5: SKEL_L_Clavicle (sol köprücük)
        21,
        // 6: SKEL_L_UpperArm (sol üst kol)
        22,
        // 7: SKEL_L_Forearm (sol ön kol)
        23,
        // 8: SKEL_L_Hand (sol el)
        24,
        // 9: SKEL_R_Clavicle (sağ köprücük)
        40,
        // 10: SKEL_R_UpperArm (sağ üst kol)
        41,
        // 11: SKEL_R_Forearm (sağ ön kol)
        42,
        // 12: SKEL_R_Hand (sağ el)
        43,
        // 13: SKEL_L_Thigh (sol uyluk)
        58,
        // 14: SKEL_L_Calf (sol baldır)
        59,
        // 15: SKEL_L_Foot (sol ayak)
        60,
        // 16: SKEL_R_Thigh (sağ uyluk)
        63,
        // 17: SKEL_R_Calf (sağ baldır)
        64,
        // 18: SKEL_R_Foot (sağ ayak)
        65,
    };

    // ── İskelet Çizgi Bağlantıları (indeksler BoneIndices dizisine göre) ──
    private static readonly (int Start, int End)[] BonePairs =
    {
        // Omurga
        (0, 1),     // kalça → göğüs
        (1, 2),     // göğüs → üst göğüs
        (2, 3),     // üst göğüs → boyun
        (3, 4),     // boyun → kafa

        // Sol kol
        (2, 5),     // üst göğüs → sol köprücük
        (5, 6),     // sol köprücük → sol üst kol
        (6, 7),     // sol üst kol → sol ön kol
        (7, 8),     // sol ön kol → sol el

        // Sağ kol
        (2, 9),     // üst göğüs → sağ köprücük
        (9, 10),    // sağ köprücük → sağ üst kol
        (10, 11),   // sağ üst kol → sağ ön kol
        (11, 12),   // sağ ön kol → sağ el

        // Sol bacak
        (0, 13),    // kalça → sol uyluk
        (13, 14),   // sol uyluk → sol baldır
        (14, 15),   // sol baldır → sol ayak

        // Sağ bacak
        (0, 16),    // kalça → sağ uyluk
        (16, 17),   // sağ uyluk → sağ baldır
        (17, 18),   // sağ baldır → sağ ayak
    };

    // ════════════════════════════════════════════════

    public ZetaSkeletonOverlay(ZetaMemory mem)
    {
        _mem = mem;
        _w2s = new WorldToScreen(mem);

        if (_mem.TargetProcess == null)
            throw new Exception("FiveM sureci bulunamadi!");

        var gfx = new Graphics()
        {
            MeasureFPS = true,
            PerPrimitiveAntiAliasing = true,
            TextAntiAliasing = true
        };

        _window = new StickyWindow(_mem.TargetProcess.MainWindowHandle, gfx)
        {
            FPS = 60,
            IsTopmost = true,
            IsVisible = true
        };

        _window.SetupGraphics += SetupGraphics;
        _window.DrawGraphics += DrawGraphics;
        _window.DestroyGraphics += DestroyGraphics;
    }

    // ── Grafik Kaynakları ──
    private void SetupGraphics(object? sender, SetupGraphicsEventArgs e)
    {
        var gfx = e.Graphics;
        _greenBrush  = gfx.CreateSolidBrush(0, 255, 70, 255);
        _yellowBrush = gfx.CreateSolidBrush(255, 255, 0, 255);
        _redBrush    = gfx.CreateSolidBrush(255, 50, 50, 255);
        _whiteBrush  = gfx.CreateSolidBrush(255, 255, 255, 200);
        _bgBrush     = gfx.CreateSolidBrush(0, 0, 0, 150);
        _font        = gfx.CreateFont("Consolas", 16, true);
        _fontSmall   = gfx.CreateFont("Consolas", 12);
    }

    private void DestroyGraphics(object? sender, DestroyGraphicsEventArgs e)
    {
        _greenBrush?.Dispose();
        _yellowBrush?.Dispose();
        _redBrush?.Dispose();
        _whiteBrush?.Dispose();
        _bgBrush?.Dispose();
        _font?.Dispose();
        _fontSmall?.Dispose();
    }

    // ══════════════════════════════════════
    //  ANA ÇİZİM DÖNGÜSÜ (Her frame çağrılır)
    // ══════════════════════════════════════
    private void DrawGraphics(object? sender, DrawGraphicsEventArgs e)
    {
        var gfx = e.Graphics;
        gfx.ClearScene();

        HandleToggleKey();

        // ── HUD Arka Plan ──
        gfx.FillRectangle(_bgBrush, 10, 10, 320, 75);

        string status = IsEspActive ? "ZETA ESP: AKTIF" : "ZETA ESP: KAPALI";
        var statusBrush = IsEspActive ? _greenBrush : _redBrush;
        gfx.DrawText(_font, statusBrush, 20, 15, status);
        gfx.DrawText(_fontSmall, _whiteBrush, 20, 38,
            $"FPS: {gfx.FPS}  |  Oyuncu: {_entityFoundCount}  |  Build: b3095");
        gfx.DrawText(_fontSmall, _whiteBrush, 20, 55, "github.com/zeta-esp");

        if (!IsEspActive) return;

        // ── ViewMatrix Güncelle ──
        _w2s.Update(_mem.BaseAddress);

        // ── World Pointer ──
        long worldPtr = _mem.Read<long>(_mem.BaseAddress + WORLD_OFFSET);
        if (worldPtr == 0)
        {
            gfx.DrawText(_font, _yellowBrush, 20, 90, "SUNUCUYA GIRIS YAPIN!");
            return;
        }

        // ── Yerel Oyuncu ──
        long localPlayer = _mem.Read<long>(worldPtr + LOCAL_PLAYER_OFFSET);

        // ── Ped Listesi (CReplayInterface üzerinden) ──
        long replayInterface = _mem.Read<long>(worldPtr + REPLAY_INTERFACE_OFFSET);
        if (replayInterface == 0) return;

        long pedInterface = _mem.Read<long>(replayInterface + PED_INTERFACE_OFFSET);
        if (pedInterface == 0) return;

        long pedList = _mem.Read<long>(pedInterface + PED_LIST_OFFSET);
        int pedMaxCount = _mem.Read<int>(pedInterface + PED_MAX_OFFSET);

        if (pedList == 0 || pedMaxCount <= 0) return;
        if (pedMaxCount > 256) pedMaxCount = 256;

        int foundCount = 0;

        // ── Her Ped İçin Döngü ──
        for (int i = 0; i < pedMaxCount; i++)
        {
            long pedPtr = _mem.Read<long>(pedList + (i * 0x10));
            if (pedPtr == 0 || pedPtr == localPlayer) continue;

            // ── Sağlık Kontrolü ──
            float health = _mem.Read<float>(pedPtr + HEALTH_OFFSET);
            if (health <= 0 || health > 1000) continue;
            float maxHealth = _mem.Read<float>(pedPtr + HEALTH_MAX_OFFSET);
            if (maxHealth <= 0) maxHealth = 200;

            // ── Skeleton Pointer Zinciri ──
            long skeletonPtr = _mem.Read<long>(pedPtr + SKELETON_OFFSET);
            if (skeletonPtr == 0) continue;

            long boneCachePtr = _mem.Read<long>(skeletonPtr + BONE_CACHE_OFFSET);
            if (boneCachePtr == 0 || boneCachePtr < 0x10000) continue;

            // ── Bone Pozisyonlarını Oku ──
            var screenBones = new Dictionary<int, (float X, float Y)>();
            bool allValid = true;

            for (int b = 0; b < BoneIndices.Length; b++)
            {
                int boneId = BoneIndices[b];
                long boneAddr = boneCachePtr + (boneId * BONE_STRIDE);

                float bx = _mem.Read<float>(boneAddr + BONE_POS_X);
                float by = _mem.Read<float>(boneAddr + BONE_POS_Y);
                float bz = _mem.Read<float>(boneAddr + BONE_POS_Z);

                // Sıfır pozisyon = geçersiz
                if (bx == 0 && by == 0 && bz == 0)
                {
                    allValid = false;
                    break;
                }

                if (_w2s.ToScreen(bx, by, bz, gfx.Width, gfx.Height,
                    out float sx, out float sy))
                {
                    screenBones[b] = (sx, sy);
                }
                else
                {
                    allValid = false;
                    break;
                }
            }

            if (!allValid || screenBones.Count != BoneIndices.Length) continue;

            // ── İskeleti Çiz ──
            foreach (var (start, end) in BonePairs)
            {
                if (screenBones.TryGetValue(start, out var s) &&
                    screenBones.TryGetValue(end, out var en))
                {
                    gfx.DrawLine(_greenBrush, s.X, s.Y, en.X, en.Y, 2);
                }
            }

            // ── Kafa Dairesi ──
            if (screenBones.TryGetValue(4, out var head))
            {
                gfx.DrawCircle(_yellowBrush, head.X, head.Y, 10, 2);
            }

            // ── HP Barı (kalçanın altında) ──
            if (screenBones.TryGetValue(0, out var pelvis) &&
                screenBones.TryGetValue(4, out var headTop))
            {
                float barWidth = 40;
                float barHeight = 4;
                float barX = pelvis.X - barWidth / 2;
                float barY = pelvis.Y + 15;
                float hpRatio = Math.Clamp(health / maxHealth, 0, 1);

                gfx.FillRectangle(_bgBrush, barX, barY, barX + barWidth, barY + barHeight);
                var hpBrush = hpRatio > 0.5f ? _greenBrush : (hpRatio > 0.25f ? _yellowBrush : _redBrush);
                gfx.FillRectangle(hpBrush, barX, barY, barX + (barWidth * hpRatio), barY + barHeight);

                // ── Mesafe Hesapla ──
                float dist = CalculateDistance(pelvis, headTop);
                if (dist > 0)
                {
                    gfx.DrawText(_fontSmall, _whiteBrush,
                        barX, barY + 8, $"{(int)health} HP");
                }
            }

            foundCount++;
        }

        _entityFoundCount = foundCount;
    }

    // ── Mesafe Tahmini (ekran piksel farkından) ──
    private float CalculateDistance((float X, float Y) a, (float X, float Y) b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    // ── Tuş Kontrolü ──
    private void HandleToggleKey()
    {
        short keyState = GetAsyncKeyState(ToggleKey);
        if ((keyState & 0x8000) > 0)
        {
            if (!_isKeyPressed)
            {
                IsEspActive = !IsEspActive;
                _isKeyPressed = true;
                Console.WriteLine($"[!] ESP: {(IsEspActive ? "AKTIF" : "KAPALI")}");
            }
        }
        else
        {
            _isKeyPressed = false;
        }
    }

    public void Run() => _window.Create();

    public void Dispose()
    {
        _window?.Dispose();
    }
}

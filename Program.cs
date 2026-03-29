using System;
using System.Threading;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            Console.Title = "Zeta | b3095 External";

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
 ███████╗███████╗████████╗ █████╗ 
 ╚══███╔╝██╔════╝╚══██╔══╝██╔══██╗
   ███╔╝ █████╗     ██║   ███████║
  ███╔╝  ██╔══╝     ██║   ██╔══██║
 ███████╗███████╗   ██║   ██║  ██║
 ╚══════╝╚══════╝   ╚═╝   ╚═╝  ╚═╝
            ");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("     [ FiveM b3095 | External ESP ]\n");
            Console.ResetColor();

            // ── Tuş Seçimi ──
            int selectedKey = 0x74;
            Console.WriteLine("[?] ESP Ac/Kapat tusu secin:");
            Console.WriteLine("  1. F5  (Varsayilan)");
            Console.WriteLine("  2. INSERT");
            Console.WriteLine("  3. NUMPAD 0");
            Console.Write("\nSeciminiz (1/2/3): ");

            string? secim = Console.ReadLine()?.Trim();
            selectedKey = secim switch
            {
                "2" => 0x2D,
                "3" => 0x60,
                _ => 0x74
            };

            string keyName = selectedKey switch
            {
                0x2D => "INSERT",
                0x60 => "NUMPAD 0",
                _ => "F5"
            };

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"[+] Tus: {keyName}\n");
            Console.ResetColor();

            // ── Bellek Motoru ──
            Console.WriteLine("[*] FiveM aranıyor...");
            ZetaMemory mem = new ZetaMemory();

            if (!mem.Baglan())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n╔══════════════════════════════════════╗");
                Console.WriteLine("║  HATA: FiveM GTAProcess BULUNAMADI  ║");
                Console.WriteLine("╠══════════════════════════════════════╣");
                Console.WriteLine("║  1. FiveM acik mi?                  ║");
                Console.WriteLine("║  2. Sunucuya girdiniz mi?           ║");
                Console.WriteLine("║  3. Yonetici olarak calistirin!     ║");
                Console.WriteLine("╚══════════════════════════════════════╝");
                Console.ResetColor();
                Console.WriteLine("\nCikmak icin ENTER...");
                Console.ReadLine();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[+] BAGLANILDI: {mem.TargetProcess!.ProcessName}");
            Console.WriteLine($"[+] PID: {mem.TargetProcess.Id}");
            Console.WriteLine($"[+] Base: 0x{mem.BaseAddress:X}");
            Console.ResetColor();

            // ── Overlay Başlat ──
            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine("\n[!] Kapatiliyor...");
            };

            using (var overlay = new ZetaSkeletonOverlay(mem))
            {
                overlay.ToggleKey = selectedKey;

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n╔══════════════════════════════════════╗");
                Console.WriteLine($"║  ZETA ESP AKTIF!                     ║");
                Console.WriteLine($"║  Tus: {keyName,-10} | Build: b3095      ║");
                Console.WriteLine($"║  Kapatmak icin: Ctrl+C               ║");
                Console.WriteLine($"╚══════════════════════════════════════╝\n");
                Console.ResetColor();

                overlay.Run();

                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        Thread.Sleep(500);

                        if (mem.TargetProcess.HasExited)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("[!] FiveM kapandi. Zeta kapatiliyor...");
                            Console.ResetColor();
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) { }
            }

            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("[+] Zeta kapatildi. Gorusuruz!");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n╔══════════════════════════════════════╗");
            Console.WriteLine("║        PROGRAM COKTU!                ║");
            Console.WriteLine("╚══════════════════════════════════════╝\n");
            Console.WriteLine(ex.ToString());
            Console.ResetColor();
            Console.WriteLine("\nENTER'a basin...");
            Console.ReadLine();
        }
    }
}

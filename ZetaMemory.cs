using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

public class ZetaMemory
{
    // ── Kernel32 Importları ──
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(
        IntPtr hProcess, long lpBaseAddress,
        [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    // ── Alanlar ──
    public IntPtr ProcessHandle;
    public long BaseAddress;
    public Process? TargetProcess;

    private const int PROCESS_ALL_ACCESS = 0x1F0FFF;

    // ── FiveM b3095 Process'ini Bul ve Bağlan ──
    public bool Baglan()
    {
        TargetProcess = Process.GetProcesses().FirstOrDefault(p =>
        {
            try
            {
                return p.ProcessName.Contains("GTAProcess", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        });

        if (TargetProcess == null) return false;

        try
        {
            ProcessHandle = OpenProcess(PROCESS_ALL_ACCESS, false, TargetProcess.Id);
            if (ProcessHandle == IntPtr.Zero) return false;

            var mainModule = TargetProcess.MainModule;
            if (mainModule == null) return false;

            BaseAddress = mainModule.BaseAddress.ToInt64();
            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[!] Erisim hatasi: {ex.Message}");
            Console.WriteLine("[!] Yonetici olarak calistirin!");
            Console.ResetColor();
            return false;
        }
    }

    // ── Genel Bellek Okuma ──
    public T Read<T>(long address) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));
        byte[] buffer = new byte[size];
        bool success = ReadProcessMemory(ProcessHandle, address, buffer, size, out _);

        if (!success) return default;

        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T))!;
        }
        finally
        {
            handle.Free();
        }
    }

    // ── Ham Byte Dizisi Okuma ──
    public byte[] ReadBytes(long address, int size)
    {
        byte[] buffer = new byte[size];
        ReadProcessMemory(ProcessHandle, address, buffer, size, out _);
        return buffer;
    }

    // ── Pointer Zinciri Takip ──
    public long ReadPointerChain(long baseAddr, params long[] offsets)
    {
        long current = baseAddr;
        for (int i = 0; i < offsets.Length; i++)
        {
            current = Read<long>(current + offsets[i]);
            if (current == 0 || current < 0x10000) return 0;
        }
        return current;
    }
}

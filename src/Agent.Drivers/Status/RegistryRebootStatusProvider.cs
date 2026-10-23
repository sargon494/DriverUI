using Core.Interfaces;
using Microsoft.Win32;

namespace Agent.Drivers.Status;

/// <summary>
/// Lee las marcas que Windows deja cuando necesita reiniciar:
/// <list type="bullet">
/// <item>Servicio de componentes (CBS): <c>Component Based Servicing\RebootPending</c>.</item>
/// <item>Windows Update: <c>WindowsUpdate\Auto Update\RebootRequired</c>.</item>
/// </list>
/// Solo lectura; no modifica el registro.
/// </summary>
public sealed class RegistryRebootStatusProvider : IRebootStatusProvider
{
    private static readonly string[] _markerKeys =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired",
    ];

    public bool IsRebootRequired()
    {
        foreach (var keyPath in _markerKeys)
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key is not null)
            {
                return true;
            }
        }

        return false;
    }
}

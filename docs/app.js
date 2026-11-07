/**
 * DriverUpdater Live Simulator & Interactive Showcase
 * Implements full 8-phase state machine & CLI diagnostic tab switcher.
 */

document.addEventListener('DOMContentLoaded', () => {
  const mobileMenu = document.querySelector('.mobile-menu');
  mobileMenu.querySelectorAll('a').forEach(link => {
    link.addEventListener('click', () => mobileMenu.removeAttribute('open'));
  });

  // ==========================================
  // 1. Data Store & State
  // ==========================================
  let isScanning = false;
  let isInstalling = false;
  let hasInstalled = false;
  let selectedDriverId = 'drv-intel-gpu';

  const drivers = [
    {
      id: 'drv-intel-gpu',
      name: 'Intel(R) Iris(R) Xe Graphics',
      hwid: 'PCI\\VEN_8086&DEV_9A49',
      currentVersion: '30.0.100.9999',
      newVersion: '31.0.101.4500',
      isWhql: true,
      signer: 'Microsoft Windows Hardware Compatibility Publisher',
      size: '284 MB',
      status: 'Update Available'
    },
    {
      id: 'drv-realtek-audio',
      name: 'Realtek High Definition Audio',
      hwid: 'HDAUDIO\\FUNC_01&VEN_10EC&DEV_0256',
      currentVersion: '6.0.9239.1',
      newVersion: '6.0.9239.1',
      isWhql: true,
      signer: 'Microsoft Windows Hardware Compatibility Publisher',
      size: '—',
      status: 'Up to Date'
    },
    {
      id: 'drv-intel-wifi',
      name: 'Intel(R) Wi-Fi 6 AX201 160MHz',
      hwid: 'PCI\\VEN_8086&DEV_02F0',
      currentVersion: '22.190.0.4',
      newVersion: '23.20.0.4',
      isWhql: true,
      signer: 'Microsoft Windows Hardware Compatibility Publisher',
      size: '48 MB',
      status: 'Update Available'
    },
    {
      id: 'drv-custom-dac',
      name: 'Audiophile USB DAC (Legacy INF)',
      hwid: 'USB\\VID_04D8&PID_FFEE',
      currentVersion: '1.0.0.0',
      newVersion: '1.2.0.0',
      isWhql: false,
      signer: 'Custom Audio Labs (Self-Signed)',
      size: '12 MB',
      status: 'Update Available (Non-WHQL)'
    }
  ];

  // DOM Elements
  const tbody = document.getElementById('sim-tbody');
  const btnScan = document.getElementById('sim-scan-btn');
  const btnInstall = document.getElementById('sim-install-btn');
  const btnRollback = document.getElementById('sim-rollback-btn');
  const terminal = document.getElementById('sim-terminal');
  const progressBar = document.getElementById('sim-progress-bar');
  const phaseLabel = document.getElementById('progress-phase-label');
  const percentLabel = document.getElementById('progress-percent-label');
  const statusPill = document.getElementById('sim-status-pill');
  const policyWhql = document.getElementById('policy-whql');
  const policyRestore = document.getElementById('policy-restore');

  // ==========================================
  // 2. Render Drivers Table
  // ==========================================
  function renderTable() {
    tbody.innerHTML = '';
    drivers.forEach(d => {
      const tr = document.createElement('tr');
      tr.className = `sim-row ${d.id === selectedDriverId ? 'selected' : ''}`;
      tr.onclick = () => selectDriver(d.id);

      const hasUpdate = d.newVersion !== d.currentVersion;
      const whqlBadge = d.isWhql
        ? '<span class="whql-badge whql-certified">✓ WHQL Certified</span>'
        : '<span class="whql-badge whql-untrusted">⚠ Non-WHQL / Self-Signed</span>';

      tr.innerHTML = `
        <td data-label="Device">
          <div class="sim-device-title">${d.name}</div>
          <div class="sim-device-hwid">${d.hwid}</div>
        </td>
        <td data-label="Current version"><span class="version-tag">${d.currentVersion}</span></td>
        <td data-label="Available update">
          ${hasUpdate 
            ? `<span class="version-tag version-new">${d.newVersion} (${d.size})</span>` 
            : `<span class="version-tag" style="opacity: 0.5;">None</span>`}
        </td>
        <td data-label="Certification">${whqlBadge}</td>
        <td data-label="Status"><span class="status-cell ${hasUpdate ? 'text-success' : 'log-muted'}">${d.status}</span></td>
      `;
      tbody.appendChild(tr);
    });

    const activeDriver = drivers.find(d => d.id === selectedDriverId);
    if (activeDriver && activeDriver.newVersion !== activeDriver.currentVersion && !isInstalling) {
      btnInstall.removeAttribute('disabled');
    } else {
      btnInstall.setAttribute('disabled', 'true');
    }
  }

  function selectDriver(id) {
    if (isInstalling) return;
    selectedDriverId = id;
    renderTable();
    const d = drivers.find(item => item.id === id);
    if (d) {
      log(`[SELECT] Selected device '${d.name}' (${d.hwid})`, 'info');
    }
  }

  // ==========================================
  // 3. Terminal Logging Helper
  // ==========================================
  function log(msg, level = 'muted') {
    const time = new Date().toISOString().substring(11, 19);
    const line = document.createElement('div');
    line.className = `log-line log-${level}`;
    line.textContent = `${time} ${msg}`;
    terminal.appendChild(line);
    terminal.scrollTop = terminal.scrollHeight;
  }

  function setProgress(phase, percent, pillState = 'active') {
    phaseLabel.textContent = phase;
    percentLabel.textContent = `${percent}%`;
    progressBar.style.width = `${percent}%`;

    statusPill.className = `status-pill status-${pillState}`;
    statusPill.textContent = pillState === 'active' ? 'IN PROGRESS' : (pillState === 'done' ? 'COMPLETE' : (pillState === 'error' ? 'ABORTED' : 'READY'));
  }

  function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
  }

  // ==========================================
  // 4. Scan Action Simulation
  // ==========================================
  btnScan.onclick = async () => {
    if (isScanning || isInstalling) return;
    isScanning = true;
    btnScan.setAttribute('disabled', 'true');
    btnInstall.setAttribute('disabled', 'true');

    log('[SCAN] Querying installed drivers via WMI (Win32_PnPSignedDriver)...', 'info');
    setProgress('Querying WMI Inventory...', 30, 'active');
    await delay(700);

    log('[SCAN] Enumerated 185 hardware drivers. Device tree mapped.', 'info');
    setProgress('Connecting to Windows Update Agent COM...', 65, 'active');
    await delay(800);

    log('[WUA] Searching catalog criterion: Type=\'Driver\' AND IsInstalled=0...', 'info');
    await delay(900);

    log('[WUA] Search complete: 2 available updates found matching hardware IDs.', 'success');
    setProgress('Scan finished. Updates available.', 100, 'done');
    await delay(400);

    isScanning = false;
    btnScan.removeAttribute('disabled');
    renderTable();
  };

  // ==========================================
  // 5. Install Action Simulation (Full 8 Phases)
  // ==========================================
  btnInstall.onclick = async () => {
    if (isInstalling) return;
    const target = drivers.find(d => d.id === selectedDriverId);
    if (!target) return;

    isInstalling = true;
    btnScan.setAttribute('disabled', 'true');
    btnInstall.setAttribute('disabled', 'true');
    btnRollback.setAttribute('disabled', 'true');

    log(`[INSTALL] Enqueuing operation for '${target.name}'...`, 'info');
    log(`[QUEUE] Acquired OperationQueue exclusive lock (SemaphoreSlim).`, 'muted');

    // FASE 1: Downloading
    setProgress(`Downloading ${target.name} (${target.size})...`, 15, 'active');
    log(`[DOWNLOAD] Fetching update payload from Windows Update CDN...`, 'info');
    await delay(1000);
    setProgress(`Download complete. Payload staged in temp storage.`, 25, 'active');
    await delay(400);

    // FASE 2: Verifying Cryptographic Signature
    setProgress(`WinVerifyTrust: Validating Authenticode & WHQL...`, 35, 'active');
    log(`[SECURITY] Invoking WinVerifyTrust with WINTRUST_ACTION_GENERIC_VERIFY_V2...`, 'info');
    await delay(800);

    if (policyWhql.checked && !target.isWhql) {
      log(`[SECURITY ERROR] File does not have WHQL certification (Signed by '${target.signer}').`, 'danger');
      log(`[SECURITY ERROR] WHQL-Only policy is ENFORCED. Aborting installation immediately.`, 'danger');
      log(`[AUDIT] Recorded failed operation in history.db (Reason: TRUST_E_NOSIGNATURE/NON_WHQL).`, 'warning');
      setProgress(`Aborted: Non-WHQL driver rejected by policy.`, 35, 'error');
      
      isInstalling = false;
      btnScan.removeAttribute('disabled');
      renderTable();
      return;
    }

    log(`[SECURITY] Authenticode verified! Signer: '${target.signer}'`, 'success');
    log(`[SECURITY] WHQL Enhanced Key Usage OID (1.3.6.1.4.1.311.10.3.5) verified.`, 'success');
    setProgress(`Signature cryptographically verified.`, 45, 'active');
    await delay(500);

    // FASE 3: Creating System Restore Point
    setProgress(`Creating System Restore Point Checkpoint...`, 55, 'active');
    log(`[RESTORE] Invoking WMI root\\default:SystemRestore.CreateRestorePoint...`, 'info');
    log(`[RESTORE] Description: 'DriverUpdater: ${target.name} (${target.newVersion})'`, 'muted');
    await delay(1100);

    if (!policyRestore.checked) {
      log(`[RESTORE ERROR] Could not create system restore point (System Restore disabled).`, 'danger');
      log(`[RESTORE ERROR] Rule 2 of AGENTS.md violated: Refusing to install without restore point.`, 'danger');
      log(`[AUDIT] Recorded abort in history.db. Hardware state unchanged.`, 'warning');
      setProgress(`Aborted: System Restore Point creation failed.`, 55, 'error');

      isInstalling = false;
      btnScan.removeAttribute('disabled');
      renderTable();
      return;
    }

    log(`[RESTORE] System Restore Point created successfully. Sequence #412.`, 'success');
    setProgress(`Restore point verified. Initiating native install...`, 65, 'active');
    await delay(500);

    // FASE 4: Installing
    setProgress(`Installing driver via pnputil /add-driver...`, 75, 'active');
    log(`[INSTALL] Staging INF package into driver store via ProcessStartInfo.ArgumentList...`, 'info');
    await delay(1200);

    // FASE 5: Validating
    setProgress(`Validating PnP device node status...`, 90, 'active');
    log(`[VALIDATE] Verifying device status in Device Tree (PnP Status: OK, Code 0)...`, 'info');
    await delay(600);

    // FASE 6: Complete
    const prevVersion = target.currentVersion;
    target.currentVersion = target.newVersion;
    target.status = 'Up to Date (Installed)';
    hasInstalled = true;

    log(`[AUDIT] Success recorded in history.db: '${target.name}' updated ${prevVersion} -> ${target.newVersion}`, 'success');
    log(`[QUEUE] OperationQueue lock released. Installation completed.`, 'info');
    setProgress(`Installation complete: ${target.name} updated.`, 100, 'done');

    isInstalling = false;
    btnScan.removeAttribute('disabled');
    btnRollback.removeAttribute('disabled');
    renderTable();
  };

  // ==========================================
  // 6. Rollback Action Simulation
  // ==========================================
  btnRollback.onclick = async () => {
    if (isInstalling || !hasInstalled) return;

    isInstalling = true;
    btnRollback.setAttribute('disabled', 'true');
    btnScan.setAttribute('disabled', 'true');

    log(`[ROLLBACK] Initiating rollback for last updated driver...`, 'warning');
    setProgress(`Querying history.db for previous driver version...`, 20, 'active');
    await delay(600);

    log(`[ROLLBACK] Target identified. Deleting driver package with pnputil /delete-driver...`, 'warning');
    setProgress(`Removing new package and rolling back hardware node...`, 60, 'active');
    await delay(1000);

    const target = drivers.find(d => d.id === selectedDriverId);
    if (target) {
      target.currentVersion = '30.0.100.9999';
      target.status = 'Update Available (Rolled Back)';
    }

    log(`[ROLLBACK] Device rolled back to 30.0.100.9999. PnP Status verified.`, 'success');
    log(`[AUDIT] Rollback recorded in history.db.`, 'info');
    setProgress(`Rollback successful. Hardware returned to previous driver.`, 100, 'done');

    hasInstalled = false;
    isInstalling = false;
    btnScan.removeAttribute('disabled');
    renderTable();
  };

  // ==========================================
  // 7. CLI Tabs Switcher
  // ==========================================
  const cliTabs = document.querySelectorAll('.cli-tab');
  const cliOutput = document.getElementById('cli-output');

  const cliSnippets = {
    scan: `<span class="prompt">PS C:\\></span> dotnet run --project src/Cli -- scan

[INF] Scanning installed drivers via WMI (Win32_PnPSignedDriver)...
[INF] Scanned 185 hardware drivers in 1.48s.

Dispositivo                             Fabricante         Versión          Fecha        Estado
--------------------------------------  -----------------  ---------------  -----------  ------
Intel(R) Iris(R) Xe Graphics            Intel Corporation  31.0.101.4500    2024-05-12   Ok
Realtek High Definition Audio           Realtek            6.0.9239.1       2023-09-18   Ok
Intel(R) Wi-Fi 6 AX201 160MHz           Intel              23.20.0.4        2024-03-02   Ok
Samsung NVMe SSD Controller             Samsung            3.3.0.1000       2022-01-15   Ok
Intel(R) Serial IO I2C Host Controller  Intel Corporation  30.100.2221.20   2023-04-10   Ok

[INF] Operation finished successfully. Total devices: 185.`,

    updates: `<span class="prompt">PS C:\\></span> dotnet run --project src/Cli -- updates

[INF] Reading installed driver inventory... (185 devices)
[INF] Querying Windows Update Agent (COM) for 'Type=Driver'...
[INF] Found 1 official update available.

Dispositivo                   Versión actual  Versión nueva  Tamaño  Título
----------------------------  --------------  -------------  ------  ---------------------------------------
Intel(R) Iris(R) Xe Graphics  30.0.100.9999   31.0.101.4500  284 MB  Intel - Display - 31.0.101.4500 (WHQL)

[INF] Run 'dotnet run --project src/Cli -- install <updateId>' to apply.`,

    log: `<span class="prompt">PS C:\\></span> dotnet run --project src/Cli -- log

[INF] Querying audit database: %ProgramData%\\DriverUpdater\\history.db

ID  Fecha (UTC)          Operación  Dispositivo                   Versión anterior  Versión nueva  Resultado
--  -------------------  ---------  ----------------------------  ----------------  -------------  ---------
1   2026-11-07 10:14:02  Install    Intel(R) Iris(R) Xe Graphics  30.0.100.9999     31.0.101.4500  Éxito
2   2026-11-07 10:22:15  Rollback   Intel(R) Iris(R) Xe Graphics  31.0.101.4500     30.0.100.9999  Éxito

[INF] Ledger integrity verified. 2 operations recorded.`
  };

  cliTabs.forEach(tab => {
    tab.addEventListener('click', () => {
      cliTabs.forEach(t => t.classList.remove('active'));
      tab.classList.add('active');
      const cmd = tab.getAttribute('data-cmd');
      cliOutput.innerHTML = `<code>${cliSnippets[cmd]}</code>`;
    });
  });

  // Initial table render
  renderTable();
});

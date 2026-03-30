import { spawn } from 'node:child_process';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const packageRoot = path.resolve(scriptDir, '..');
const repoRoot = path.resolve(packageRoot, '..');
const bridgeProject = path.join(repoRoot, 'bridge', 'SolidWorksBridge', 'SolidWorksBridge.csproj');
const bridgeReleaseExe = path.join(
  repoRoot,
  'bridge',
  'SolidWorksBridge',
  'bin',
  'Release',
  'net8.0-windows',
  'win-x64',
  'SolidWorksBridge.exe',
);
const bridgeDebugExe = path.join(
  repoRoot,
  'bridge',
  'SolidWorksBridge',
  'bin',
  'Debug',
  'net8.0-windows',
  'win-x64',
  'SolidWorksBridge.exe',
);
const publishDir = path.join(
  repoRoot,
  'bridge',
  'SolidWorksBridge',
  'bin',
  'Release',
  'net8.0-windows',
  'win-x64',
  'publish',
);
const vendorDir = path.join(packageRoot, 'vendor', 'bridge');
const vendorBridgeExe = path.join(vendorDir, 'SolidWorksBridge.exe');
const WINDOWS_POWERSHELL = 'C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe';

function run(command, args, cwd) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd,
      stdio: 'inherit',
      shell: false,
    });

    child.once('error', reject);
    child.once('exit', (code) => {
      if (code === 0) {
        resolve();
        return;
      }

      reject(new Error(`${command} ${args.join(' ')} exited with code ${code ?? -1}`));
    });
  });
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function isRetriableCopyError(error) {
  return Boolean(
    error
    && typeof error === 'object'
    && 'code' in error
    && (error.code === 'EBUSY' || error.code === 'EPERM'),
  );
}

async function stopPackagedBridgeIfRunning() {
  if (process.platform !== 'win32') {
    return;
  }

  const trackedExecutables = [bridgeReleaseExe, bridgeDebugExe, vendorBridgeExe]
    .map((target) => `[System.IO.Path]::GetFullPath('${target.replace(/'/g, "''")}')`)
    .join(', ');

  const stopCommand = [
    `$targets = @(${trackedExecutables})`,
    "$processes = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'SolidWorksBridge.exe' -and $_.ExecutablePath -and $targets -contains [System.IO.Path]::GetFullPath($_.ExecutablePath) }",
    'foreach ($process in $processes) { Stop-Process -Id $process.ProcessId -Force }',
  ].join('; ');

  await run(
    WINDOWS_POWERSHELL,
    ['-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-Command', stopCommand],
    repoRoot,
  );
}

async function listFiles(rootDir, currentDir = rootDir) {
  const entries = await fs.readdir(currentDir, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const entryPath = path.join(currentDir, entry.name);
    if (entry.isDirectory()) {
      files.push(...await listFiles(rootDir, entryPath));
      continue;
    }

    if (entry.isFile()) {
      files.push(path.relative(rootDir, entryPath));
    }
  }

  return files;
}

async function fileContentsMatch(sourcePath, targetPath) {
  try {
    const [sourceContent, targetContent] = await Promise.all([
      fs.readFile(sourcePath),
      fs.readFile(targetPath),
    ]);

    return sourceContent.equals(targetContent);
  } catch (error) {
    if (error && typeof error === 'object' && 'code' in error && error.code === 'ENOENT') {
      return false;
    }

    throw error;
  }
}

async function copyFileWithRetry(sourcePath, targetPath, maxAttempts = 10) {
  for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
    try {
      await fs.copyFile(sourcePath, targetPath);
      return;
    } catch (error) {
      if (!isRetriableCopyError(error) || attempt === maxAttempts) {
        throw error;
      }

      await sleep(200 * attempt);
    }
  }
}

async function syncPublishOutput(sourceDir, targetDir) {
  const relativeFiles = await listFiles(sourceDir);

  for (const relativeFile of relativeFiles) {
    const sourcePath = path.join(sourceDir, relativeFile);
    const targetPath = path.join(targetDir, relativeFile);

    await fs.mkdir(path.dirname(targetPath), { recursive: true });

    if (await fileContentsMatch(sourcePath, targetPath)) {
      continue;
    }

    await copyFileWithRetry(sourcePath, targetPath);
  }
}

await stopPackagedBridgeIfRunning();
await run(
  'dotnet',
  ['publish', bridgeProject, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false'],
  repoRoot,
);

await fs.mkdir(vendorDir, { recursive: true });
await syncPublishOutput(publishDir, vendorDir);

console.log(`Bundled SolidWorks bridge into ${vendorDir}`);
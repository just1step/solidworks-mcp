import { spawn } from 'node:child_process';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const packageRoot = path.resolve(scriptDir, '..');
const repoRoot = path.resolve(packageRoot, '..');
const bridgeProject = path.join(repoRoot, 'bridge', 'SolidWorksBridge', 'SolidWorksBridge.csproj');
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

await run(
  'dotnet',
  ['publish', bridgeProject, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false'],
  repoRoot,
);

await fs.rm(vendorDir, { recursive: true, force: true });
await fs.mkdir(vendorDir, { recursive: true });
await fs.cp(publishDir, vendorDir, { recursive: true });

console.log(`Bundled SolidWorks bridge into ${vendorDir}`);
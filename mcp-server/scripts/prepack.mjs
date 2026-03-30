import { spawn } from 'node:child_process';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const packageRoot = path.resolve(scriptDir, '..');
const nodeExecPath = process.env.npm_node_execpath || process.execPath;

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

await run(nodeExecPath, ['./node_modules/typescript/bin/tsc'], packageRoot);
await run(nodeExecPath, ['./scripts/bundle-bridge.mjs'], packageRoot);
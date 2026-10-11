"""Check the actual project launchers using MCP initialize and tools/list."""
import json
import os
from pathlib import Path
import queue
import shutil
import subprocess
import threading

ROOT = Path(__file__).resolve().parents[2]


def verify(tool):
    shell = shutil.which('pwsh')
    if not shell:
        raise RuntimeError('PowerShell 7 (pwsh) is required')
    env = dict(os.environ, CODEGRAPH_TELEMETRY='0', PYTHONUTF8='1')
    with (ROOT / '.analysis' / f'{tool}-mcp-stderr.log').open('w', encoding='utf-8') as err:
        process = subprocess.Popen(
            [shell, '-NoProfile', '-File', str(ROOT / 'tools/analysis/mcp.ps1'), '-Tool', tool],
            cwd=ROOT, env=env, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            stderr=err, text=True, encoding='utf-8', creationflags=0x08000000 if os.name == 'nt' else 0,
        )
        messages = queue.Queue()

        def read():
            for line in process.stdout:
                try:
                    messages.put(json.loads(line))
                except ValueError:
                    messages.put({'unexpectedStdout': line.rstrip()})

        threading.Thread(target=read, daemon=True).start()

        def request(number, method, params):
            process.stdin.write(json.dumps({'jsonrpc': '2.0', 'id': number, 'method': method, 'params': params}) + '\n')
            process.stdin.flush()
            while True:
                response = messages.get(timeout=45)
                if 'unexpectedStdout' in response:
                    raise RuntimeError(response)
                if response.get('id') == number:
                    if 'error' in response:
                        raise RuntimeError(response['error'])
                    return response['result']

        try:
            initialized = request(1, 'initialize', {'protocolVersion': '2024-11-05', 'capabilities': {}, 'clientInfo': {'name': 'DeepRim-analysis-check', 'version': '1.0'}})
            process.stdin.write(json.dumps({'jsonrpc': '2.0', 'method': 'notifications/initialized'}) + '\n')
            process.stdin.flush()
            available = request(2, 'tools/list', {})['tools']
            names = {item['name'] for item in available}
            required = {'codegraph_explore'} if tool == 'codegraph' else {'scan_plan', 'inspect'}
            if not required <= names:
                raise RuntimeError(f'Missing tools: {required - names}')
            if tool == 'codegraph':
                call = request(3, 'tools/call', {'name': 'codegraph_explore', 'arguments': {'query': 'DirtyRenderingMapCells', 'projectPath': str(ROOT)}})
                if call.get('isError'):
                    raise RuntimeError(call)
            else:
                call = request(3, 'tools/call', {'name': 'scan_plan', 'arguments': {'project_path': str(ROOT), 'scope': 'Source'}})
                if call.get('isError'):
                    raise RuntimeError(call)
            return {'tool': tool, 'server': initialized.get('serverInfo'), 'tools': sorted(names), 'sampleCall': 'ok'}
        finally:
            process.stdin.close()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.terminate()
                process.wait(timeout=5)


if __name__ == '__main__':
    (ROOT / '.analysis').mkdir(exist_ok=True)
    results = [verify(tool) for tool in ('codegraph', 'codeslicer')]
    (ROOT / '.analysis/mcp-verification.json').write_text(json.dumps(results, indent=2, ensure_ascii=False), encoding='utf-8')
    for result in results:
        print(f"{result['tool']}: initialize, tools/list, tools/call OK ({len(result['tools'])} tools)")

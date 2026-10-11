"""Export a compact baseline without source snippets or machine-specific paths."""
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return json.loads((ROOT / path).read_text(encoding='utf-8-sig'))


def main():
    graph = read('.analysis/codeslicer-graph.json')
    metadata = graph['metadata']
    sample = read('.analysis/render-invalidation-slice.json')
    engine = read('.analysis/engine/manifest.json')
    checked = read('.analysis/mcp-verification.json')
    versions = read('tools/analysis/versions.json')
    report = {
        'auditDate': '2026-10-11',
        'sourceCommit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
        'scope': 'Source',
        'versions': versions,
        'engine': {'gameVersion': engine['gameVersion'], 'assemblySha256': engine['assemblySha256'], 'typesInspected': len(engine['types'])},
        'codeslicer': {
            'nodes': len(graph['nodes']), 'edges': len(graph['edges']),
            'provider': metadata['csharp_provider'],
            'counts': metadata['csharp_counts'],
            'callResolution': metadata['resolution_coverage']['totals'],
            'integrity': read('.analysis/quality.json'),
            'slice': {'target': 'method:DeepRim.Vertical.VerticalRendering.VerticalSectionRenderPatches.DirtyRenderingMapCells', 'matchedNodes': len(sample['matched_nodes']), 'affectedNodes': len(sample['affected_nodes']), 'affectedEdges': len(sample['affected_edges'])},
        },
        'mcp': [{'tool': item['tool'], 'advertisedTools': len(item['tools']), 'initialize': 'ok', 'toolsList': 'ok', 'sampleCall': item['sampleCall']} for item in checked],
        'validation': {'releaseBuild': {'errors': 0, 'warnings': 0}, 'inGameProfiling': 'not performed', 'note': 'Graph integrity does not prove C# call resolution. Baseline precedes the documentation/tooling commit.'},
    }
    destination = ROOT / 'docs/analysis-baseline.json'
    destination.write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print('Exported docs/analysis-baseline.json (no game code or local paths)')


if __name__ == '__main__':
    main()

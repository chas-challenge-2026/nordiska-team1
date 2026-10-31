"""Architecture guard plus process-level privacy regressions using synthetic data."""
import json
import os
from pathlib import Path
import re
import subprocess
import socket
import struct
from concurrent.futures import ThreadPoolExecutor
import sys
import tempfile

root = Path(__file__).resolve().parents[1]
cli = Path(sys.argv[1]).resolve()
marker = 'PRIVATE_CUSTOMER_918273645'
# Application output is confined to the typed CLI adapter. PDF serialization
# and returning completed artifacts to the caller are separate data paths.
for directory in ('src', 'include', 'cli'):
    for source in (root / directory).rglob('*'):
        if source.suffix not in ('.cpp', '.hpp', '.h'):
            continue
        text = source.read_text()
        assert not re.search(r'\bwhat\s*\(', text), f'exception text access in {source}'
        if source == root / 'cli/output.cpp':
            continue
        assert not re.search(
            r'std::(?:cout|cerr|clog)|\b(?:printf|fprintf|puts|fputs|perror|syslog|vprintf|vfprintf|writev|fwrite)\s*\(',
            text), f'output bypass in {source}'
        assert not re.search(r'\b(?:fmt::print|spdlog::|std::print)', text), f'logging bypass in {source}'
# Trivial copyability alone would allow string_view and raw text pointers.
for relative, names in {
    'include/nordiska/diagnostics/diagnostic.hpp': ['Diagnostic', 'DiagnosticLocation'],
    'include/nordiska/ingestion/json_ingestor.hpp': ['IngestError'],
    'include/nordiska/layout/layout_builder.hpp': ['LayoutError'],
    'include/nordiska/rendering/pdf_engine.hpp': ['RenderError'],
    'include/nordiska/signing/pdf_signer.hpp': ['SigningError'],
    'include/nordiska/application/pdf_generator.hpp': ['InvalidArgumentError', 'ResourceLimitError', 'InternalError'],
}.items():
    text = (root / relative).read_text()
    for name in names:
        declaration = re.search(r'\bstruct ' + name + r' \{(.*?)\n\};', text, re.S)
        assert declaration, f'missing diagnostic declaration: {name}'
        fields = declaration[1].split('[[nodiscard]]')[0]
        assert not re.search(r'\b(?:string|string_view|span)\b|\*|&', fields), f'text or pointer in {name}'

# The only diagnostic encoder must accept a structured value, never text.
api = (root / 'src/c_api/pdf_generator_c_api.cpp').read_text()
assert 'void set_last_error(nordiska::Diagnostic diagnostic) noexcept' in api
assert 'set_last_error_static' not in api

payload = {'customer_id': 918273645, 'customer_name': marker, 'documents': [
    {'document_id': marker, 'kind': 'account_statement', 'document': {
        'account_number': marker, 'account_name': marker, 'title': marker,
        'transactions': [{'date': marker, 'description': marker}]}}]}
env = dict(os.environ, NORDISKA_PDF_ENABLE_SIGNING='0', NORDISKA_PDF_ENGINE='native')
def run(args, data=None, success=None, extra_env=None):
    result = subprocess.run([str(cli), *args], input=data, text=True, capture_output=True,
                            env={**env, **(extra_env or {})}, timeout=20)
    assert result.returncode >= 0, 'CLI terminated by a signal' 
    assert marker not in result.stdout + result.stderr, 'customer text escaped CLI output'
    assert '918273645' not in result.stdout + result.stderr, 'customer ID escaped CLI output'
    if success is not None:
        assert (result.returncode == 0) == success, (result.returncode, result.stdout, result.stderr)
    return result

with tempfile.TemporaryDirectory() as temporary:
    output = Path(temporary) / marker
    for renderer in ('native', 'haru', 'cairo'):
        for ingestor in ('simdjson', 'nlohmann'):
            for mode in ([], ['--verbose'], ['--json-summary'], ['--quiet']):
                result = run(['-', '-o', str(output), '-r', renderer, '-e', ingestor, *mode], json.dumps(payload), True)
                assert (output / f'{marker}.pdf').read_bytes().startswith(b'%PDF-'), 'artifact not delivered'
                if '--json-summary' in mode:
                    summary = json.loads(result.stdout)
                    assert summary['documents'][0]['document_index'] == 0
                    assert 'customer_id' not in summary
                    assert 'document_id' not in summary['documents'][0]
                    assert 'file_path' not in summary['documents'][0]
            malformed = '{"customer_name":"' + marker + '","bad":}'
            for mode in ([], ['--json-summary'], ['--quiet']):
                run(['-', '-o', str(output), '-e', ingestor, *mode], malformed, False)
            invalid = json.loads(json.dumps(payload))
            invalid['documents'][0]['kind'] = marker
            run(['-', '-o', str(output), '-e', ingestor, '--json-summary'], json.dumps(invalid), False)
            invalid['documents'][0]['kind'] = 'account_statement'
            invalid['documents'].append(invalid['documents'][0])
            run(['-', '-o', str(output), '-e', ingestor], json.dumps(invalid), False)
    # Usage, I/O and filesystem exceptions must not echo paths or values.
    for option in ('--renderer', '--ingestor', '--compression', '--signature-capacity', '--logo', '--profile'):
        run([option, marker], success=False)
    run(['--profile', 'white'], success=False)
    run(['--' + marker], success=False)
    run(['-i', str(output / marker)], success=False)
    blocked = Path(temporary) / f'{marker}.file'
    blocked.write_text('blocked')
    run(['-', '-o', str(blocked / marker)], json.dumps(payload), False)
# Exercise the actual generator socket adapter; no external signer process or
# private keys are required. A timestamp failure is status 4 in protocol v2.
responses = {
    'truncated-header': b'\x00\x00',
    'empty': struct.pack('!II', 0, 0),
    'oversized': struct.pack('!II', 0, 65537),
    'truncated-body': struct.pack('!II', 0, 30) + b'30',
    'private-invalid-hex': struct.pack('!II', 0, len(marker)) + marker.encode(),
    **{f'status-{status}': struct.pack('!II', status, 0) for status in (1, 2, 3, 4, 5, 918273645)},
}
with tempfile.TemporaryDirectory() as temporary, ThreadPoolExecutor(max_workers=1) as executor:
    base = Path(temporary)
    # A missing socket path must not be echoed, even if it contains private data.
    for mode in ('plain', 'timestamp'):
        destination = base / (mode + '-missing')
        result = run(['-', '--signing', '-o', str(destination), '--json-summary'], json.dumps(payload), False,
                     {'PDF_SIGNER_SOCKET_PATH': str(base / marker), 'PDF_SIGNER_MODE': mode})
        assert result.returncode == 5
        assert not list(destination.glob('*.pdf')), 'signing failure wrote an artifact'
        for name, response in responses.items():
            socket_path = base / 'signer.sock'
            destination = base / (mode + '-' + name)
            with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as server:
                server.bind(str(socket_path))
                server.listen(1)
                server.settimeout(10)
                def respond():
                    connection, _ = server.accept()
                    with connection:
                        connection.settimeout(10)
                        request = b''
                        while len(request) < 48:
                            part = connection.recv(48 - len(request))
                            assert part, 'generator truncated the digest request'
                            request += part
                        assert struct.unpack('!IIII', request[:16]) == (2, 1, 32, int(mode == 'timestamp'))
                        assert len(request[16:]) == 32, 'signer received something other than a SHA-256 digest'
                        # Reject any identity/context appended after the fixed request.
                        connection.setblocking(False)
                        try:
                            extra = connection.recv(1)
                            assert not extra, 'generator appended data to the digest request'
                        except BlockingIOError:
                            pass
                        connection.setblocking(True)
                        connection.sendall(response)
                future = executor.submit(respond)
                result = run(['-', '--signing', '-o', str(destination), '--json-summary'], json.dumps(payload), False,
                             {'PDF_SIGNER_SOCKET_PATH': str(socket_path), 'PDF_SIGNER_MODE': mode})
                future.result(timeout=10)
                assert result.returncode == 5, 'signing failure must return the signing exit code'
                assert json.loads(result.stdout)['status'] == 'error'
                assert not list(destination.glob('*.pdf')), 'failed signing wrote an artifact'
            socket_path.unlink()
print('Diagnostic output boundary and CLI privacy checks passed')

#!/usr/bin/env bash
# Windows için tek dosyalık VeriCat.exe üretir (macOS/Linux üzerinden çapraz derleme).
# Kullanım: ./build/publish.sh   ->  artifacts/publish/win-x64/VeriCat.exe
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet test VeriCat.slnx -c Release --nologo -v quiet
dotnet publish src/VeriCat.Desktop/VeriCat.Desktop.csproj -p:PublishProfile=win-x64 --nologo -v quiet

exe="artifacts/publish/win-x64/VeriCat.exe"

# Windows dışında derlenince SDK exe'yi "konsol programı" olarak bırakıyor (NETSDK1074).
# PE başlığındaki alt sistem alanını GUI (2) yap ki siyah konsol penceresi açılmasın.
python3 - "$exe" <<'EOF'
import struct, sys
path = sys.argv[1]
with open(path, 'r+b') as f:
    head = f.read(4096)
    pe = struct.unpack_from('<I', head, 0x3C)[0]
    assert head[pe:pe + 4] == b'PE\0\0', 'PE imzası bulunamadı'
    field = pe + 24 + 68   # IMAGE_OPTIONAL_HEADER64.Subsystem
    f.seek(field)
    f.write(struct.pack('<H', 2))
print('alt sistem: GUI')
EOF

echo "Hazır: $(pwd)/$exe"

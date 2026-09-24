#!/usr/bin/env bash
# Windows için tek dosyalık VeriCat.exe ve SHA-256 özetini üretir (macOS/Linux üzerinden çapraz derleme).
#
# Kullanım:
#   ./build/publish.sh            -> geliştirici sürümü (0.0.0, kendini güncellemez)
#   ./build/publish.sh 1.4.0      -> sürüm numaralı derleme (CI'da semantic-release çağırır)
#   SKIP_TESTS=1 ./build/publish.sh 1.4.0
#
# Çıktı: artifacts/release/VeriCat.exe, artifacts/release/VeriCat.exe.sha256
set -euo pipefail
cd "$(dirname "$0")/.."

version="${1:-0.0.0}"
out="artifacts/release"

if [[ "${SKIP_TESTS:-0}" != "1" ]]; then
  dotnet test VeriCat.slnx -c Release --nologo -v quiet
fi

dotnet publish src/VeriCat.Desktop/VeriCat.Desktop.csproj -p:PublishProfile=win-x64 -p:Version="$version" --nologo -v quiet

mkdir -p "$out"
cp artifacts/publish/win-x64/VeriCat.exe "$out/VeriCat.exe"
exe="$out/VeriCat.exe"

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

# Özet, alt sistem düzeltmesinden SONRA alınır: uygulama indirdiği dosyayı buna göre doğrular.
(cd "$out" && sha256sum VeriCat.exe > VeriCat.exe.sha256)

echo "Hazır: $(pwd)/$exe (v$version)"
cat "$out/VeriCat.exe.sha256"

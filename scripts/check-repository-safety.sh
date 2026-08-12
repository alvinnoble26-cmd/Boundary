#!/bin/sh
set -eu

if git diff --cached --name-only | grep -Eiq '(^|/)(\.env($|\.)|.*service.?account.*\.json$)|\.(pem|key|p12|p8|mobileprovision)$'; then
  echo "Refusing commit: a probable credential or signing file is staged."
  exit 1
fi

if git diff --cached --no-ext-diff --unified=0 | grep -Eiq 'BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY|EDGEGAP_API_TOKEN[[:space:]]*='; then
  echo "Refusing commit: probable private key or Edgegap token content is staged."
  exit 1
fi

oversized="$(find . -type f -size +95000000c -not -path './.git/*' -print | while IFS= read -r path; do
  path="${path#./}"
  git diff --cached --name-only --diff-filter=ACM -- "$path" | grep -q . || continue
  filter="$(git check-attr filter -- "$path" | sed 's/.*: //')"
  [ "$filter" = "lfs" ] && continue
  bytes="$(wc -c < "$path" | tr -d ' ')"
  echo "$path ($bytes bytes)"
done)"

if [ -n "$oversized" ]; then
  echo "Refusing commit: GitHub rejects files near or above 100 MB:"
  echo "$oversized"
  exit 1
fi

echo "Repository safety checks passed."

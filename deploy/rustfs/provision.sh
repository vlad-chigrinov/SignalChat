#!/bin/sh
# One-time (and safe to repeat) setup of the RustFS object store: for one environment it creates the media bucket,
# a policy that allows exactly that bucket and nothing else, and an access key bound to the policy. Run it inside the
# rustfs/rc image on the signalchat-data network, see docs/deploy.md ("RustFS: first start").
#
# Required environment: RUSTFS_ENDPOINT, RUSTFS_ROOT_USER, RUSTFS_ROOT_PASSWORD (the root keys of the store),
# MEDIA_BUCKET, MEDIA_ACCESS_KEY, MEDIA_SECRET_KEY (what the API of that environment will use).
set -eu

for name in RUSTFS_ENDPOINT RUSTFS_ROOT_USER RUSTFS_ROOT_PASSWORD MEDIA_BUCKET MEDIA_ACCESS_KEY MEDIA_SECRET_KEY; do
  eval "value=\${$name:-}"
  if [ -z "$value" ]; then
    echo "error: $name is not set" >&2
    exit 1
  fi
done

alias_name=signalchat
policy_name="${MEDIA_BUCKET}-readwrite"
policy_file="$(mktemp)"
trap 'rm -f "$policy_file"' EXIT

cat > "$policy_file" <<POLICY
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:GetBucketLocation", "s3:ListBucket"],
      "Resource": ["arn:aws:s3:::${MEDIA_BUCKET}"]
    },
    {
      "Effect": "Allow",
      "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject", "s3:AbortMultipartUpload"],
      "Resource": ["arn:aws:s3:::${MEDIA_BUCKET}/*"]
    }
  ]
}
POLICY

rc alias set "$alias_name" "$RUSTFS_ENDPOINT" "$RUSTFS_ROOT_USER" "$RUSTFS_ROOT_PASSWORD"
rc bucket create --ignore-existing "$alias_name/$MEDIA_BUCKET"

# Policy and user are created if missing; on a repeat run the existing ones are kept (rotate a key through the console).
if ! rc admin policy info "$alias_name" "$policy_name" >/dev/null 2>&1; then
  rc admin policy create "$alias_name" "$policy_name" "$policy_file"
fi
if ! rc admin user info "$alias_name" "$MEDIA_ACCESS_KEY" >/dev/null 2>&1; then
  rc admin user add "$alias_name" "$MEDIA_ACCESS_KEY" "$MEDIA_SECRET_KEY"
fi
rc admin policy attach "$alias_name" "$policy_name" --user "$MEDIA_ACCESS_KEY"

echo "RustFS ready: bucket $MEDIA_BUCKET, access key $MEDIA_ACCESS_KEY bound to policy $policy_name."

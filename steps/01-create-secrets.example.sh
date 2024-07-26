export PING_TUNNEL_TOKEN="change_me"
export PG_PROD_PING_API="change_me"


cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: Secret
metadata:
  name: ping-infra
  namespace: production
stringData:
  tunnel-token: ${PING_TUNNEL_TOKEN}
EOF

cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: Secret
metadata:
  name: ping-infra
  namespace: staging
stringData:
  tunnel-token: ${PING_TUNNEL_TOKEN}
EOF

cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: Secret
metadata:
  name: maindb-ping
  namespace: production
stringData:
  ping-api-username: pingapi
  ping-api-password: ${PG_PROD_PING_API}
EOF

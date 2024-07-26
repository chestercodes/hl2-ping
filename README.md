# hl2-ping

## pipeline flow

- main is pushed and v|date| tag is pushed
- triggers workflow, asks to proceed then runs migrations and updates version in staging file, updates env-staging tag, end2end tests run after sync
- staging tag triggers pipeline, waits then looks up v|date| tag at same commit
- runs migrations and updates production version, is then updated and smoke tests run

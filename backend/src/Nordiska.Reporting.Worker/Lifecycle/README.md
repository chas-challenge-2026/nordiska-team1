# Lifecycle & Recovery

`WorkerLifecycle` tracks `Starting`, `Running`, `Draining`, and `Stopped`.
During draining, the worker wakes idle slots and waits for active work before
cancelling their tokens. The default drain limit is 25 seconds; the Docker
worker has a 35-second stop grace period.

This module does not renew job leases. Existing claim queries in
`Modules/Reporting` make a job eligible again after its lease expires if a
worker dies or its drain limit is reached.

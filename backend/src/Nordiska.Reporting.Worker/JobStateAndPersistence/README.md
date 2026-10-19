# Job State & Persistence

The persistent job state currently belongs to `Modules/Reporting`, which is
shared by the API and this worker. The worker consumes its job repository and
completion contracts; it must not maintain a second copy of job status or
lease data here.

Current implementations include the tax report and account statement job
entities, claim repositories, completion repositories, `ReportingDbContext`,
and report document storage in `../../Modules/Reporting`.

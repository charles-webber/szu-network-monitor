# Third-party notices

## `nnothing1/szu-srun-login`

The Go login helper in `src/SzuLoginHelper` is a modified derivative of the SRun login implementation from [`nnothing1/szu-srun-login`](https://github.com/nnothing1/szu-srun-login), commit `f2eab7efc434330403b268a2b2519cc9346cb47a`.

Upstream copyright is retained by its contributors. Upstream is licensed under the GNU Affero General Public License, version 3. This project changes the transport to dial the campus authentication server's configured IP while retaining TLS hostname verification, returns structured result data, reads the password from standard input, and fixes unsuccessful portal responses to produce non-zero failures.

The full corresponding source for this derivative is included in this repository under `src/SzuLoginHelper`.

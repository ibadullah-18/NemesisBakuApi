# Showcase tests

Run `dotnet test NemesisBakuApi.Tests/NemesisBakuApi.Tests.csproj --configuration Release`.

Tests use an isolated in-memory SQLite database and fake image/audit services.
They do not start the production host, migrate a real database, upload to
Cloudinary, or use customer data. Coverage includes group creation and updates,
adding/removing/reordering blocks, page visibility, product ordering, stale edit
versions, reserved/duplicate slugs, URL validation, and failed-upload cleanup.

The SQL Server migration is generated separately and checked against the EF
model. Real Cloudinary uploads and a deployed SQL Server migration still require
the normal deployment smoke check.

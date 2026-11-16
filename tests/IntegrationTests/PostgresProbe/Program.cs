using System.Text.Json;
using Business.Platform.Core.Infrastructure.Persistence;
using Business.Platform.Core.Domain.Tenancy;
using Npgsql;
// Controlled product integration probe, never a G3 protocol or authorization issuer.
var checks = new Dictionary<string,bool>();
try {
 var input = JsonSerializer.Deserialize<PostgresOptions>(await Console.In.ReadToEndAsync())!;
 var config = new NpgsqlConnectionStringBuilder(input.ConnectionString()) { MaxPoolSize=1 };
 await using var pool = NpgsqlDataSource.Create(config.ConnectionString);
 var reader = new PostgresEnterpriseContextReader(pool);
 checks["migrationAndCatalog"] = await reader.CheckProvisionedAsync(CancellationToken.None);
 var subject = new OidcSubject("https://synthetic.invalid","npgsql-"+Guid.NewGuid());
 await reader.RegisterAsync(subject,CancellationToken.None);
 var snapshot = await reader.ReadAsync(subject,CancellationToken.None);
 checks["realNpgsqlRegistrationAndRead"] = snapshot.IsSuccess && snapshot.Value.Bindings.Count==0;
 int first;
 await using(var c=await pool.OpenConnectionAsync()) {
  first=(int)(await new NpgsqlCommand("SELECT pg_backend_pid()",c).ExecuteScalarAsync())!;
  await using var tx=await c.BeginTransactionAsync();
  await using var cmd=new NpgsqlCommand("SELECT bpc_security.resolve_context($1,$2)::text",c,tx);
  cmd.Parameters.AddWithValue(subject.Issuer);cmd.Parameters.AddWithValue(subject.Subject);
  await cmd.ExecuteScalarAsync();await tx.CommitAsync();
 }
 await using(var c=await pool.OpenConnectionAsync()) {
  var pid=(int)(await new NpgsqlCommand("SELECT pg_backend_pid()",c).ExecuteScalarAsync())!;
  checks["sameBackendAfterCommit"] = first==pid;
  checks["contextClearedAfterCommit"] = await new NpgsqlCommand("SELECT nullif(current_setting('app.identity_id',true),'') IS NULL AND nullif(current_setting('app.organization_id',true),'') IS NULL",c).ExecuteScalarAsync() is true;
  await using var tx=await c.BeginTransactionAsync();
  await using var cmd=new NpgsqlCommand("SELECT bpc_security.resolve_context($1,$2)::text",c,tx);
  cmd.Parameters.AddWithValue(subject.Issuer);cmd.Parameters.AddWithValue(subject.Subject);
  await cmd.ExecuteScalarAsync();await tx.RollbackAsync();
 }
 await using(var c=await pool.OpenConnectionAsync()) {
  checks["sameBackendAfterRollback"] = first==(int)(await new NpgsqlCommand("SELECT pg_backend_pid()",c).ExecuteScalarAsync())!;
  checks["contextClearedAfterRollback"] = await new NpgsqlCommand("SELECT nullif(current_setting('app.identity_id',true),'') IS NULL AND nullif(current_setting('app.organization_id',true),'') IS NULL",c).ExecuteScalarAsync() is true;
  try { await new NpgsqlCommand("SELECT * FROM tenancy.memberships",c).ExecuteNonQueryAsync();checks["directReadDenied"]=false; }
  catch(PostgresException e) when(e.SqlState=="42501") { checks["directReadDenied"]=true; }
  await using var tx=await c.BeginTransactionAsync();
  using var cancel=new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
  try { await new NpgsqlCommand("SELECT pg_sleep(10)",c,tx).ExecuteNonQueryAsync(cancel.Token);checks["cancellation"]=false; }
  catch(OperationCanceledException) { checks["cancellation"]=true; }
  await tx.RollbackAsync();
 }
 await using(var c=await pool.OpenConnectionAsync()) {
  checks["usableAfterCancellation"] = await new NpgsqlCommand("SELECT 1",c).ExecuteScalarAsync() is 1;
  checks["contextClearedAfterCancellation"] = await new NpgsqlCommand("SELECT nullif(current_setting('app.identity_id',true),'') IS NULL",c).ExecuteScalarAsync() is true;
 }
 var passed=checks.Values.All(x=>x);
 Console.WriteLine(JsonSerializer.Serialize(new {passed,checks,g3Executed=false}));
 return passed?0:1;
} catch(Exception e) { Console.WriteLine(JsonSerializer.Serialize(new {passed=false,errorType=e.GetType().Name,checks}));return 1; }

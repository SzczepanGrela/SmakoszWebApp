using Xunit;

// Respawn resets state between tests on a shared Postgres container; running tests in parallel would cause one test to wipe data another is mid-flight using.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

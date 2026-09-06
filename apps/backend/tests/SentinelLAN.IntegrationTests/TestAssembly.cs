// Factories may target the same explicitly configured PostgreSQL test database.
// Keep fixture seeding sequential; individual tests still exercise concurrent requests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

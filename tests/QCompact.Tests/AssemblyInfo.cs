// Tests swap process-global Console streams (Logger/Capture helpers), so
// classes must not run concurrently.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
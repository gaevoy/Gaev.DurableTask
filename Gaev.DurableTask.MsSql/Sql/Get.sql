SELECT IsException, [State]
FROM DurableTasks
WHERE ProcessId = @ProcessId AND OperationId = @OperationId
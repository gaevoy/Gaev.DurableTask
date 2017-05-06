MERGE DurableTasks
AS dst
USING (SELECT @ProcessId, @OperationId, @IsException, @State)
AS src (ProcessId, OperationId, IsException, [State])
ON (dst.ProcessId = src.ProcessId AND dst.OperationId = src.OperationId)
	WHEN MATCHED 
		THEN UPDATE SET dst.IsException = src.IsException, dst.[State] = src.[State]
	WHEN NOT MATCHED
		THEN INSERT VALUES (ProcessId, OperationId, IsException, [State]);
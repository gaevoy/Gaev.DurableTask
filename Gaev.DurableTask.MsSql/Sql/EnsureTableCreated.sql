IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DurableTasks]') AND type in (N'U'))
BEGIN
	CREATE TABLE [dbo].[DurableTasks](
		[Id] [int] IDENTITY(1,1) NOT NULL,
		[ProcessId] [nvarchar](255) NOT NULL,
		[OperationId] [nvarchar](255) NOT NULL,
		[IsException] [bit] NOT NULL,
		[State] [nvarchar](max) NOT NULL,
	 CONSTRAINT [PK_DurableTasks] PRIMARY KEY CLUSTERED 
	(
		[Id] ASC
	)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
	) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END
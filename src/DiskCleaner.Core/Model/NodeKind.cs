namespace DiskCleaner.Core.Model;

public enum NodeKind { File, Directory }

[Flags]
public enum NodeFlags { None = 0, AccessDenied = 1, ReparsePoint = 2 }

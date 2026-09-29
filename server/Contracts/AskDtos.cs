namespace Huamishu.Api.Contracts;

public sealed record AskRequest(string Question);
public sealed record AskResponse(string Answer);
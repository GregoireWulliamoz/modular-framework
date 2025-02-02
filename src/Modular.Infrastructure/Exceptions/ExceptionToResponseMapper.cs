﻿using System.Collections.Concurrent;
using System.Net;
using Humanizer;
using Modular.Abstractions.Exceptions;

namespace Modular.Infrastructure.Exceptions;

public class ExceptionToResponseMapper : IExceptionToResponseMapper
{
    private static readonly ConcurrentDictionary<Type, string> Codes = new();

    private static string GetErrorCode(object exception)
    {
        Type type = exception.GetType();
        return Codes.GetOrAdd(type, type.Name.Underscore().Replace("_exception", string.Empty));
    }

    public ExceptionResponse Map(Exception exception)
        => exception switch
        {
            ModularException ex => new ExceptionResponse(new ErrorsResponse(new Error(GetErrorCode(ex), ex.Message))
                , HttpStatusCode.BadRequest),
            _ => new ExceptionResponse(new ErrorsResponse(new Error("error", "There was an error.")),
                HttpStatusCode.InternalServerError)
        };

    private sealed record Error(string Code, string Message);

    private sealed record ErrorsResponse(params Error[] Errors);
}
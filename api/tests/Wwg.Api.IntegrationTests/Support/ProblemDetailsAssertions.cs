using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Wwg.Api.IntegrationTests.Support;

internal static class ProblemDetailsAssertions
{
    /// <summary>Asserts a Problem Details response with the given status, and returns it.</summary>
    public static async Task<ProblemDetails> AssertProblemAsync(
        this HttpResponseMessage response,
        HttpStatusCode status
    )
    {
        AssertProblemResponse(response, status);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(problem);
        Assert.Equal((int)status, problem.Status);
        return problem;
    }

    /// <summary>
    /// Asserts a 400 validation problem with errors for exactly the given (camelCase) fields, and
    /// returns it.
    /// </summary>
    public static async Task<HttpValidationProblemDetails> AssertValidationProblemAsync(
        this HttpResponseMessage response,
        params string[] fields
    )
    {
        AssertProblemResponse(response, HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(problem);
        Assert.Equal(
            fields.Order(StringComparer.Ordinal),
            problem.Errors.Keys.Order(StringComparer.Ordinal)
        );
        return problem;
    }

    private static void AssertProblemResponse(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}

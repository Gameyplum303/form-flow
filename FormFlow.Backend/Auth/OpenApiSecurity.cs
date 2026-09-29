using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FormFlow.Backend.Auth
{
    /// <summary>Adds the bearer token to the OpenAPI document.</summary>
    public static class OpenApiSecurity
    {
        private const string SchemeName = "Bearer";

        /// <summary>
        /// Describes the bearer token in the OpenAPI document and marks the endpoints that need it,
        /// so Swagger UI shows an Authorize button and a lock on admin endpoints.
        /// </summary>
        public static OpenApiOptions AddBearerTokenSecurity(this OpenApiOptions options)
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Paste the token from POST /api/auth/login.",
                };
                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [],
                    });
                }
                return Task.CompletedTask;
            });

            return options;
        }
    }
}

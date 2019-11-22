﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Xunit;

namespace Microsoft.Extensions.DependencyInjection
{
    public class ApiBehaviorOptionsSetupTest
    {
        [Fact]
        public void Configure_AddsClientErrorMappings()
        {
            // Arrange
            var expected = new[] { 400, 401, 403, 404, 406, 409, 415, 422, 500, };
            var optionsSetup = new ApiBehaviorOptionsSetup();
            var options = new ApiBehaviorOptions();

            // Act
            optionsSetup.Configure(options);

            // Assert
            Assert.Equal(expected, options.ClientErrorMapping.Keys);
        }

        [Fact]
        public void ProblemDetailsInvalidModelStateResponse_ReturnsBadRequestWithProblemDetails()
        {
            // Arrange
            var actionContext = GetActionContext();
            var factory = GetProblemDetailsFactory();

            // Act
            var result = ApiBehaviorOptionsSetup.ProblemDetailsInvalidModelStateResponse(factory, actionContext);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(new[] { "application/problem+json", "application/problem+xml" }, badRequest.ContentTypes.OrderBy(c => c));

            var problemDetails = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
            Assert.Equal(400, problemDetails.Status);
            Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
            Assert.Equal("https://tools.ietf.org/html/rfc7231#section-6.5.1", problemDetails.Type);
        }

        [Fact]
        public void ProblemDetailsInvalidModelStateResponse_ReturnsObjectResultWithProblemDetails()
        {
            // Arrange
            var actionContext = GetActionContext();
            var factory = GetProblemDetailsFactory();

            // Act
            var result = ApiBehaviorOptionsSetup.ProblemDetailsInvalidModelStateResponse(factory, actionContext);

            // Assert
            var badRequest = Assert.IsType<ObjectResult>(result);
            Assert.Equal(422, result.StatusCode);
            Assert.Equal(new[] { "application/problem+json", "application/problem+xml" }, badRequest.ContentTypes.OrderBy(c => c));

            var problemDetails = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
            Assert.Equal(422, problemDetails.Status);
            Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
            Assert.Equal("https://tools.ietf.org/html/rfc7231#section-6.5.1", problemDetails.Type);
        }

        [Fact]
        public void ProblemDetailsInvalidModelStateResponse_UsesUserConfiguredLink()
        {
            // Arrange
            var link = "http://mylink";
            var actionContext = GetActionContext();

            var factory = GetProblemDetailsFactory(options => options.ClientErrorMapping[400].Link = link);

            // Act
            var result = ApiBehaviorOptionsSetup.ProblemDetailsInvalidModelStateResponse(factory, actionContext);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(new[] { "application/problem+json", "application/problem+xml" }, badRequest.ContentTypes.OrderBy(c => c));

            var problemDetails = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
            Assert.Equal(400, problemDetails.Status);
            Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
            Assert.Equal(link, problemDetails.Type);
        }

        [Fact]
        public void ProblemDetailsInvalidModelStateResponse_SetsTraceId()
        {
            // Arrange
            using (new ActivityReplacer())
            {
                var actionContext = GetActionContext();
                var factory = GetProblemDetailsFactory();

                // Act
                var result = ApiBehaviorOptionsSetup.ProblemDetailsInvalidModelStateResponse(factory, actionContext);

                // Assert
                var badRequest = Assert.IsType<BadRequestObjectResult>(result);
                var problemDetails = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
                Assert.Equal(Activity.Current.Id, problemDetails.Extensions["traceId"]);
            }
        }

        [Fact]
        public void ProblemDetailsInvalidModelStateResponse_SetsTraceIdFromRequest_IfActivityIsNull()
        {
            // Arrange
            var actionContext = GetActionContext();
            var factory = GetProblemDetailsFactory();

            // Act
            var result = ApiBehaviorOptionsSetup.ProblemDetailsInvalidModelStateResponse(factory, actionContext);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var problemDetails = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
            Assert.Equal("42", problemDetails.Extensions["traceId"]);
        }

        private static ProblemDetailsFactory GetProblemDetailsFactory(Action<ApiBehaviorOptions> configure = null)
        {
            var options = new ApiBehaviorOptions();
            var setup = new ApiBehaviorOptionsSetup();

            setup.Configure(options);
            if (configure != null)
            {
                configure(options);
            }

            return new DefaultProblemDetailsFactory(Options.Options.Create(options));
        }

        private static ProblemDetailsFactory GetCustomProblemDetailsFactory()
        {
            var options = new ApiBehaviorOptions();
            var setup = new ApiBehaviorOptionsSetup();

            return new CustomProblemDetailsFactory();
        }

        private class CustomProblemDetailsFactory : ProblemDetailsFactory
        {
            public override ProblemDetails CreateProblemDetails(
                HttpContext httpContext,
                int? statusCode = null,
                string title = null,
                string type = null,
                string detail = null,
                string instance = null)
            {
                statusCode ??= 500;

                var problemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = title,
                    Type = type,
                    Detail = detail,
                    Instance = instance,
                };

                return problemDetails;
            }

            public override ValidationProblemDetails CreateValidationProblemDetails(
                HttpContext httpContext,
                ModelStateDictionary modelStateDictionary,
                int? statusCode = null,
                string title = null,
                string type = null,
                string detail = null,
                string instance = null)
            {
                if (modelStateDictionary == null)
                {
                    throw new ArgumentNullException(nameof(modelStateDictionary));
                }

                statusCode ??= 422;

                var problemDetails = new ValidationProblemDetails(modelStateDictionary)
                {
                    Status = statusCode,
                    Type = type,
                    Detail = detail,
                    Instance = instance,
                };

                if (title != null)
                {
                    // For validation problem details, don't overwrite the default title with null.
                    problemDetails.Title = title;
                }

                return problemDetails;
            }
        }

        private static ActionContext GetActionContext()
        {
            return new ActionContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "42" },
            };
        }
    }
}

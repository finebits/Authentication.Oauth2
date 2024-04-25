// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2024 Finebits (https://finebits.com/)                            //
//                                                                              //
//   Licensed under the Apache License, Version 2.0 (the "License"),            //
//   you may not use this file except in compliance with the License.           //
//   You may obtain a copy of the License at                                    //
//                                                                              //
//       http://www.apache.org/licenses/LICENSE-2.0                             //
//                                                                              //
//   Unless required by applicable law or agreed to in writing, software        //
//   distributed under the License is distributed on an "AS IS" BASIS,          //
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.   //
//   See the License for the specific language governing permissions and        //
//   limitations under the License.                                             //
//                                                                              //
// ---------------------------------------------------------------------------- //

using System.Diagnostics.CodeAnalysis;
using System.Net;

using Finebits.Authorization.OAuth2.Abstractions;
using Finebits.Authorization.OAuth2.Exceptions;
using Finebits.Authorization.OAuth2.Microsoft;
using Finebits.Authorization.OAuth2.Test.Data.Mocks;

using Moq;

namespace Finebits.Authorization.OAuth2.Test.AuthClientTests;

[SuppressMessage("Performance", "CA1812: Avoid uninstantiated internal classes", Justification = "Class is instantiated via NUnit Framework")]
[TestFixtureSource(typeof(Test.Data.AuthClientDataFixture), nameof(Test.Data.AuthClientDataFixture.UserAvatarLoaderFixtureData))]
internal class AuthClientLoadAvatarTests
{
    private Test.Data.AuthClientType AuthType { get; init; }

    public AuthClientLoadAvatarTests(Test.Data.AuthClientType authType)
    {
        AuthType = authType;
    }

    [Test]
    public void LoadAvatarAsync_NullParam_Exception()
    {
        var mockHttpClient = new Mock<HttpClient>();
        var mockAuthBroker = new Mock<IAuthenticationBroker>();
        var config = Test.Data.AuthCreator.CreateConfig(AuthType);
        var client = Test.Data.AuthCreator.CreateAuthClient(AuthType, mockHttpClient.Object, mockAuthBroker.Object, config);

        var userAvatarLoader = client as IUserAvatarLoader;
        Assert.That(userAvatarLoader, Is.Not.Null);

        var exception = Assert.ThrowsAsync<ArgumentNullException>(async () => await userAvatarLoader.LoadAvatarAsync(null).ConfigureAwait(false));
        Assert.That(exception.ParamName, Is.EqualTo("token"));
    }

    [Test]
    public void LoadAvatarAsync_CorrectRequest_Success()
    {
        using var httpClient = new HttpClient(HttpMessageHandlerCreator.CreateSuccess().Object);
        var mockAuthBroker = new Mock<IAuthenticationBroker>();
        var config = Test.Data.AuthCreator.CreateConfig(AuthType);
        var client = Test.Data.AuthCreator.CreateAuthClient(AuthType, httpClient, mockAuthBroker.Object, config);
        var token = Test.Data.AuthCreator.CreateFakeToken();

        var userAvatarLoader = client as IUserAvatarLoader;
        Assert.That(userAvatarLoader, Is.Not.Null);

        Stream? stream = null;
        Assert.DoesNotThrowAsync(async () => stream = await userAvatarLoader.LoadAvatarAsync(token).ConfigureAwait(false));
        Assert.That(stream, Is.Not.Null);

        using StreamReader reader = new(stream);
        string avatar = reader.ReadToEnd();
        Assert.That(avatar, Is.EqualTo(FakeConstant.UserProfile.Avatar));
    }

    [Test]
    public void LoadAvatarAsync_CancellationToken_Exception()
    {
        using var cts = new CancellationTokenSource();
        using var httpClient = new HttpClient(HttpMessageHandlerCreator.CreateSuccess().Object);
        var mockAuthBroker = new Mock<IAuthenticationBroker>();
        var config = Test.Data.AuthCreator.CreateConfig(AuthType);
        var client = Test.Data.AuthCreator.CreateAuthClient(AuthType, httpClient, mockAuthBroker.Object, config);
        var token = Test.Data.AuthCreator.CreateFakeToken();

        var userAvatarLoader = client as IUserAvatarLoader;
        Assert.That(userAvatarLoader, Is.Not.Null);

        cts.Cancel();
        var exception = Assert.CatchAsync<OperationCanceledException>(async () => await userAvatarLoader.LoadAvatarAsync(token, cts.Token).ConfigureAwait(false));
        Assert.That(exception, Is.Not.Null);
    }

    [Test]
    public void LoadAvatarAsync_RequestCancellationToken_Exception()
    {
        using var cts = new CancellationTokenSource();
        using var httpClient = new HttpClient(HttpMessageHandlerCreator.CreateCancellationToken(cts).Object);
        var mockAuthBroker = new Mock<IAuthenticationBroker>();
        var config = Test.Data.AuthCreator.CreateConfig(AuthType);
        var client = Test.Data.AuthCreator.CreateAuthClient(AuthType, httpClient, mockAuthBroker.Object, config);
        var token = Test.Data.AuthCreator.CreateFakeToken();

        var userAvatarLoader = client as IUserAvatarLoader;
        Assert.That(userAvatarLoader, Is.Not.Null);

        var exception = Assert.CatchAsync<OperationCanceledException>(async () => await userAvatarLoader.LoadAvatarAsync(token, cts.Token).ConfigureAwait(false));
        Assert.That(exception, Is.Not.Null);
    }

    [Test]
    public void LoadAvatarAsync_HttpInvalidResponse_Exception()
    {
        using var httpClient = new HttpClient(HttpMessageHandlerCreator.CreateInvalidResponse().Object);
        var mockAuthBroker = new Mock<IAuthenticationBroker>();
        var config = Test.Data.AuthCreator.CreateConfig(AuthType);
        var client = Test.Data.AuthCreator.CreateAuthClient(AuthType, httpClient, mockAuthBroker.Object, config);
        var token = Test.Data.AuthCreator.CreateFakeToken();

        var userAvatarLoader = client as IUserAvatarLoader;
        Assert.That(userAvatarLoader, Is.Not.Null);

        var exception = Assert.ThrowsAsync<AuthorizationInvalidResponseException>(async () => await userAvatarLoader.LoadAvatarAsync(token).ConfigureAwait(false));

        Assert.That(exception, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(exception.ErrorReason, Is.EqualTo(FakeConstant.Error));
            Assert.That(exception.ErrorDescription, Is.EqualTo(FakeConstant.ErrorDescription));
            Assert.That(exception.ResponseDetails, Is.Not.Null);
            Assert.That(exception.ResponseDetails is IMicrosoftInvalidResponse, client is MicrosoftAuthClient ? Is.True : Is.False);
        });

        var innerException = exception.InnerException as HttpRequestException;
        Assert.That(innerException, Is.Not.Null);
        Assert.That(innerException.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public void LoadAvatarAsync_HttpBadRequest_Exception()
    {
        using var httpClient = new HttpClient(HttpMessageHandlerCreator.CreateHttpError().Object);
        var mockAuthBroker = new Mock<IAuthenticationBroker>();
        var config = Test.Data.AuthCreator.CreateConfig(AuthType);
        var client = Test.Data.AuthCreator.CreateAuthClient(AuthType, httpClient, mockAuthBroker.Object, config);
        var token = Test.Data.AuthCreator.CreateFakeToken();

        var userAvatarLoader = client as IUserAvatarLoader;
        Assert.That(userAvatarLoader, Is.Not.Null);

        var exception = Assert.ThrowsAsync<AuthorizationInvalidResponseException>(async () => await userAvatarLoader.LoadAvatarAsync(token).ConfigureAwait(false));

        Assert.That(exception, Is.Not.Null);
        var innerException = exception.InnerException as HttpRequestException;
        Assert.That(innerException, Is.Not.Null);
        Assert.That(innerException.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public void LoadAvatarAsync_HttpEmptyContent_Exception()
    {
        using var httpClient = new HttpClient(HttpMessageHandlerCreator.CreateEmptyResponse().Object);
        var mockAuthBroker = new Mock<IAuthenticationBroker>();
        var config = Test.Data.AuthCreator.CreateConfig(AuthType);
        var client = Test.Data.AuthCreator.CreateAuthClient(AuthType, httpClient, mockAuthBroker.Object, config);
        var token = Test.Data.AuthCreator.CreateFakeToken();

        var userAvatarLoader = client as IUserAvatarLoader;
        Assert.That(userAvatarLoader, Is.Not.Null);

        var exception = Assert.ThrowsAsync<AuthorizationDownloadFileException>(async () => await userAvatarLoader.LoadAvatarAsync(token).ConfigureAwait(false));

        Assert.That(exception, Is.Not.Null);
    }
}

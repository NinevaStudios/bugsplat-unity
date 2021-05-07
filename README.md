[![BugSplat](https://s3.amazonaws.com/bugsplat-public/npm/header.png)](https://www.bugsplat.com)

[![openupm](https://img.shields.io/npm/v/com.bugsplat.unity?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.bugsplat.unity/)

## Introduction
BugSplat's `com.bugsplat.unity` package provides crash and exception reporting for Unity projects. BugSplat provides you with invaluable insight into the issues tripping up your users. Our Unity integration collects screenshots, log files, exceptions, and Windows minidumps so that you can fix bugs and deliver a better user experience.

## Prerequisites
In order to use this package please make sure you have completed the following checklist:
* [Sign Up](https://app.bugsplat.com/v2/sign-up) as a new BugSplat user
* [Log In](https://app.bugsplat.com/auth0/login) to your account

Additionally, you can check out our [my-unity-crasher](https://github.com/BugSplat-Git/my-unity-crasher) sample that demonstrates how to use `com.bugsplat.unity`.

## Installation
BugSplat's `com.bugsplat.unity` package can be added to your project via [OpenUPM](https://openupm.com/packages/com.bugsplat.unity/) or a URL to our git [repository](https://github.com/BugSplat-Git/bugsplat-unity.git).

### OpenUPM
Information on how to install the OpenUPM package for node.js can be found [here](https://openupm.com).

```sh
openupm add com.bugsplat.unity
```

### Git
Information on adding a Unity package via a git URL can be found [here](https://docs.unity3d.com/Manual/upm-ui-giturl.html).

```sh
https://github.com/BugSplat-Git/bugsplat-unity.git
```

## Usage

BugSplat's Unity integration is flexible and can be used in a variety of ways. The easiest way to get started is to create new script and attach it to a GameObject. In your script, add a using statement that aliases `BugSplatUnity.BugSplat` as `BugSplat`.

```cs
using BugSplat = BugSplatUnity.BugSplat;
```

Next, create a new instance of `BugSplat` passing it your `database`, `application`, and `version`. Use `Application.productName`, and `Application.version` for application and version respectively.

```cs
var bugsplat = new BugSplat(database, Application.productName, Application.version);
```

You can set the defaults for a variety of properties on the `BugSplat` instance. These default values will be used in exception and crash posts. Additionally you can tell BugSplat to capture a screenshot, include the Player.log file, and include the Editor.log file when an exception is recorded.

```cs
bugsplat.Attachments.Add(new FileInfo("/path/to/attachment.txt"));
bugsplat.Description = "description!";
bugsplat.Email = "fred@bugsplat.com";
bugsplat.Key = "key!";
bugsplat.User = "Fred";
bugsplat.CaptureEditorLog = true;
bugsplat.CapturePlayerLog = false;
bugsplat.CaptureScreenshots = true;
```

You can send exceptions to BugSplat in a try/catch block by calling `Post`.

```cs
try
{
    throw new Exception("BugSplat rocks!");
}
catch (Exception ex)
{
    StartCoroutine(bugsplat.Post(ex));
}
```

The default values specified on the instance of `BugSplat` can be overridden in the call to `Post`. Additionally, you can provide a `callback` to `Post` that will be invoked with the result once the upload is complete.

```cs
var options = new ExceptionPostOptions()
{
    Description = "a new description",
    Email = "barney@bugsplat.com",
    Key = "a new key!",
    User = "Barney"
};

options.AdditionalAttachments.Add(new FileInfo("/path/to/additional.txt"));

static async void callback(HttpResponseMessage response)
{
    var status = response.StatusCode;
    var contents = await response.Content.ReadAsStringAsync();
    Debug.Log($"Response {status}: {contents}");
};

StartCoroutine(bugsplat.Post(ex, options, callback));
```

You can also configure a global `LogMessageRecieved` callback. When the BugSplat instance recieves a logging event where the type is `Exception` it will upload the exception.

```cs
Application.logMessageReceived += bugsplat.LogMessageReceived;
```

BugSplat can be configured to upload Windows minidumps created by the `UnityCrashHandler`. If your game contains Native Windows C++ plugins, `.exe`, `.dll` and `.pdb` files in the `Assets/Plugins/x86` and `Assets/Plugins/x86_64` folders they will be uploaded by BugSplat's PostBuild script and used in symbolication.

```cs
StartCoroutine(bugsplat.PostCrash(new FileInfo("/path/to/crash/folder")));
StartCoroutine(bugsplat.PostMostRecentCrash());
StartCoroutine(bugsplat.PostAllCrashes());
```

Each of the methods that post crashes to BugSplat also accept a `MinidumpPostOptions` parameter and a `callback`. The usage of `MinidumpPostOptions` and `callback` are nearly identically to the `ExceptionPostOptions` example listed above.

You can generate a test crash on Windows with any of the following methods.

```cs
Utils.ForceCrash(ForcedCrashCategory.Abort);
Utils.ForceCrash(ForcedCrashCategory.AccessViolation);
Utils.ForceCrash(ForcedCrashCategory.FatalError);
Utils.ForceCrash(ForcedCrashCategory.PureVirtualFunction);
```

Once you've posted an exception or a minidump to BugSplat click the link in the **ID** column on either the [Dashboard](https://app.bugsplat.com/v2/dashboard) or [Crashes](https://app.bugsplat.com/v2/crashes) pages to see details about your crash.

![BugSplat crash page](https://bugsplat-public.s3.amazonaws.com/unity/my-unity-crasher.png)

## Contributing

BugSplat ❤️s open source! If you feel that this integration can be improved, please open an [Issue](https://github.com/BugSplat-Git/bugsplat-unity/issues). If you have an awesome new feature you'd like to implement, we'd love to merge your [Pull Request](https://github.com/BugSplat-Git/bugsplat-unity/pulls). You can also reach out to us via an email to support@bugsplat.com or the in-app chat on bugsplat.com.

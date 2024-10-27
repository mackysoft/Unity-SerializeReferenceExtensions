# Unity SerializeReferenceExtensions

[![Build](https://github.com/mackysoft/Unity-SerializeReferenceExtensions/actions/workflows/build.yaml/badge.svg)](https://github.com/mackysoft/Unity-SerializeReferenceExtensions/actions/workflows/build.yaml) [![Release](https://img.shields.io/github/v/release/mackysoft/Unity-SerializeReferenceExtensions)](https://github.com/mackysoft/Unity-SerializeReferenceExtensions/releases) [![openupm](https://img.shields.io/npm/v/com.mackysoft.serializereference-extensions?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.mackysoft.serializereference-extensions/)

**Inspired by [this post](https://qiita.com/tsukimi_neko/items/7922b2433ed4d8616cce).**

The `SerializeReference` attribute, added in Unity 2019.3, makes it possible to serialize references to interfaces and abstract classes.

The `SubclassSelector` attribute allows you to easily set subclasses of those abstract classes in the Editor that are serialized by `SerializeReference` attribute.

![SubclassSelector](https://user-images.githubusercontent.com/13536348/118233552-03cd1780-b4cd-11eb-9e5b-4824e8f01f1d.gif)

#### Features

- Easily set subclass by popup.
- **[New]** Type finding by fuzzy finder.
- **[New]** Override the type name and path by the `AddTypeMenu` attribute.
- **[New]** Available `CustomPropertyDrawer` for subclasses.
- **[New]** Restore values of previous object from JSON when subclass is changed. (required Unity 2021.3 or later)
- **[New]** Copy & Paste the subclass properties. (required Unity 2021.3 or later)
- **[New]** Clear & reset the subclass properties. (required Unity 2021.3 or later)
- **[New]** Covariance / Contravariance support. (required Unity 2023.2 or later)

> See below for the reason for the limitation of versions less than Unity 2021.3.
> 
> https://blog.unity.com/engine-platform/serializereference-improvements-in-unity-2021-lts

## 📥 Installation

#### Install via `.unitypackage`

Download any version from releases.

Releases: https://github.com/mackysoft/Unity-SerializeReferenceExtensions/releases

#### Install via git URL

Or, you can add this package by opening PackageManager and entering

```
https://github.com/mackysoft/Unity-SerializeReferenceExtensions.git?path=Assets/MackySoft/MackySoft.SerializeReferenceExtensions
```

from the `Add package from git URL` option.

If you are specifying a version, enter `#{VERSION}` at the end, as shown below.

```
https://github.com/mackysoft/Unity-SerializeReferenceExtensions.git?path=Assets/MackySoft/MackySoft.SerializeReferenceExtensions#1.1.9
```

#### Install via Open UPM

Or, you can install this package from the [Open UPM](https://openupm.com/packages/com.mackysoft.serializereference-extensions/) registry.

More details [here](https://openupm.com/).

```
openupm add com.mackysoft.serializereference-extensions
```

## 🔰 Usage

```cs
using System;
using UnityEngine;

public class Example : MonoBehaviour {

	// The type that implements ICommand will be displayed in the popup.
	[SerializeReference, SubclassSelector]
	ICommand m_Command;

	// Collection support
	[SerializeReference, SubclassSelector]
	ICommand[] m_Commands = Array.Empty<ICommand>();

	void Start () {
		m_Command?.Execute();

		foreach (ICommand command in m_Commands) {
			command?.Execute();
		}
	}

	// Nested type support
	[Serializable]
	public class NestedCommand : ICommand {
		public void Execute () {
			Debug.Log("Execute NestedCommand");
		}
	}

}

public interface ICommand {
	void Execute ();
}

[Serializable]
public class DebugCommand : ICommand {

	[SerializeField]
	string m_Message;

	public void Execute () {
		Debug.Log(m_Message);
	}
}

[Serializable]
public class InstantiateCommand : ICommand {

	[SerializeField]
	GameObject m_Prefab;

	public void Execute () {
		UnityEngine.Object.Instantiate(m_Prefab,Vector3.zero,Quaternion.identity);
	}
}

// Menu override support
[AddTypeMenu("Example/Add Type Menu Command")]
[Serializable]
public class AddTypeMenuCommand : ICommand {
	public void Execute () {
		Debug.Log("Execute AddTypeMenuCommand");
	}
}

[Serializable]
public struct StructCommand : ICommand {
	public void Execute () {
		Debug.Log("Execute StructCommand");
	}
}
```

#### Supported Types

The `SubclassSelector` attribute supports types that meet the following conditions.

- Public or nested private
- Not abstract
- Not generic
- Not unity object
- Serializable attribute is applied.


## ❓ FAQ

### If the type is renamed, the reference is lost.

It is a limitation of `SerializeReference` of Unity.

When serializing a `SerializeReference` reference, the type name, namespace, and assembly name are used, so if any of these are changed, the reference cannot be resolved during deserialization.

To solve this problem, `UnityEngine.Scripting.APIUpdating.MovedFromAttribute` can be used.

Also, [this thread](https://forum.unity.com/threads/serializereference-data-loss-when-class-name-is-changed.736874/) will be helpful.

#### References
- https://forum.unity.com/threads/serializereference-data-loss-when-class-name-is-changed.736874/
- https://issuetracker.unity3d.com/issues/serializereference-serialized-reference-data-lost-when-the-class-name-is-refactored

# <a id="help-and-contribute" href="#help-and-contribute"> ✉ Help & Contribute </a>

I welcome feature requests and bug reports in [issues](https://github.com/mackysoft/XPool/issues) and [pull requests](https://github.com/mackysoft/XPool/pulls).

If you feel that my works are worthwhile, I would greatly appreciate it if you could sponsor me.

GitHub Sponsors: https://github.com/sponsors/mackysoft

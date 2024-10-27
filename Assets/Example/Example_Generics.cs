using System;
using System.Collections.Generic;
using UnityEngine;

public interface IActor { }
public interface IStandardActor : IActor { }
public interface INetworkActor : IActor { }

public interface IContravarianceAction<in T> where T : IActor {
	void DoAction (T actor);
}

public interface ICovarianceAction<out T> where T : IActor
{
	T Actor { get; }
}

public interface IActorAction : IContravarianceAction<IActor>, ICovarianceAction<IActor> { }
public interface IStandardActorAction : IContravarianceAction<IStandardActor>, ICovarianceAction<IStandardActor> { }
public interface INetworkActorAction : IContravarianceAction<INetworkActor>, ICovarianceAction<INetworkActor> { }

[Serializable]
public sealed class StandardActorAction : IContravarianceAction<IStandardActor>, ICovarianceAction<IStandardActor>
{
	public void DoAction (IStandardActor actor)
	{
	}
	public IStandardActor Actor => null;
}

[Serializable]
public sealed class ActorAction : IContravarianceAction<IActor>, ICovarianceAction<IActor>
{
	public void DoAction (IActor actor)
	{
	}
	public IActor Actor => null;
}

[Serializable]
public abstract class BaseAction<T> : IContravarianceAction<T>, ICovarianceAction<T> where T : IActor
{
	public void DoAction (T actor) {
	}
	public T Actor => default;
}

[Serializable]
public sealed class DerivedAction1 : BaseAction<IActor> { }

[Serializable]
public sealed class DerivedAction2 : BaseAction<INetworkActor> { }

[Serializable]
public sealed class DerivedAction3 : BaseAction<IStandardActor> { }

[Serializable]
public sealed class NetworkActorAction1 : INetworkActorAction
{
	public void DoAction (INetworkActor actor)
	{
	}
	public INetworkActor Actor => null;
}

[Serializable]
public sealed class NetworkActorAction2 : IContravarianceAction<INetworkActor>, ICovarianceAction<INetworkActor>
{
	public void DoAction (INetworkActor actor)
	{
	}
	public INetworkActor Actor => null;
}

[Serializable]
public sealed class NetworkActorAction3 : IContravarianceAction<IActor>, ICovarianceAction<IActor>
{
	public void DoAction (IActor actor)
	{
	}
	public IActor Actor => null;
}

public class Example_Generics : MonoBehaviour
{

	[SerializeReference, SubclassSelector]
	public List<IContravarianceAction<INetworkActor>> contravarianceActions = new List<IContravarianceAction<INetworkActor>>();

	[SerializeReference, SubclassSelector]
	public List<ICovarianceAction<INetworkActor>> covarianceActions = new List<ICovarianceAction<INetworkActor>>();

}

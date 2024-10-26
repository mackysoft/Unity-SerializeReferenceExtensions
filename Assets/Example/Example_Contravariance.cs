using System;
using System.Collections.Generic;
using UnityEngine;

public interface IActor { }
public interface IStandardActor : IActor { }
public interface INetworkActor : IActor { }
public interface IAction<in T> where T : IActor { }

public interface IActorAction : IAction<IActor> { }
public interface IStandardActorAction : IAction<IStandardActor> { }
public interface INetworkActorAction : IAction<INetworkActor> { }

[Serializable]
public sealed class NetworkActorAction : INetworkActorAction { }

public class Example_Contravariance : MonoBehaviour
{

	[SerializeReference, SubclassSelector]
	public List<IAction<INetworkActor>> actions = new List<IAction<INetworkActor>>();

}

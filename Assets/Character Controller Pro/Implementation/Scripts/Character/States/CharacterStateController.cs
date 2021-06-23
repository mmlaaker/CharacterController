using System.Collections.Generic;
using UnityEngine;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.Utilities;

namespace Lightbug.CharacterControllerPro.Implementation
{

/// <summary>
/// Interface used for objects that need to be updated in a frame by frame basis.
/// </summary>
public interface IUpdatable
{
	void PreUpdateBehaviour( float dt );
	void UpdateBehaviour( float dt );
	void PostUpdateBehaviour( float dt );
}


/// <summary>
/// This class handles all the involved states from the character, allowing an organized execution of events. It also contains extra information that may be required and shared between all the states.
/// </summary>
[AddComponentMenu("Character Controller Pro/Implementation/Character/Character State Controller")]
public sealed class CharacterStateController : MonoBehaviour
{
	
	[SerializeField]
	CharacterState currentState = null;

	[CustomClassDrawer]
	[SerializeField]
    MovementReferenceParameters movementReferenceParameters = new MovementReferenceParameters();

	
	


	// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
	// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
	
	/// <summary>
	/// Gets the Animator component associated with the state controller.
	/// </summary>
	public Animator Animator => CharacterActor.Animator;

	public bool UseRootMotion
	{
		get => CharacterActor.UseRootMotion;
		set => CharacterActor.UseRootMotion = value;		
	}

	public bool UpdateRootPosition
	{
		get => CharacterActor.UpdateRootPosition;
		set => CharacterActor.UpdateRootPosition = value;		
	}

	public bool UpdateRootRotation
	{
		get => CharacterActor.UpdateRootRotation;
		set => CharacterActor.UpdateRootRotation = value;		
	}


	Dictionary< string , CharacterState > states = new Dictionary< string , CharacterState >();
	
	CharacterState previousState = null;


	/// <summary>
	/// Gets the brain component associated with the state controller.
	/// </summary>
	public CharacterActor CharacterActor{ get; private set; }
	

	/// <summary>
	/// Gets the brain component associated with the state controller.
	/// </summary>
	public CharacterBrain CharacterBrain{ get; private set; }


	/// <summary>
	/// This event is called when a state transition occurs. 
	/// 
	/// The "from" and "to" states are passed as arguments.
	/// </summary>
	public event System.Action< CharacterState , CharacterState > OnStateChange;

	

	/// <summary>
	/// Gets the current state used by the state machine.
	/// </summary>
	public CharacterState CurrentState
	{
		get
		{ 
			return currentState;
		} 
	}  

	/// <summary>
	/// Gets the previous state used by the state machine.
	/// </summary>
	public CharacterState PreviousState
	{
		get
		{ 
			return previousState;
		} 
	} 

	
	
	/// <summary>
	/// Searches for a particular state.
	/// </summary>
	public CharacterState GetState( string stateName )
	{
		CharacterState state = null;
		bool validState = states.TryGetValue( stateName , out state );

		return state;
	}

	/// <summary>
	/// Searches for a particular state.
	/// </summary>
	public CharacterState GetState<T>() where T : CharacterState
	{
		foreach( var state in states.Values )
		{
			if( state.GetType() == typeof( T ) )
				return state;
			
		}
		
		return null;
	}

	/// <summary>
	/// Adds a particular state to the transition state queue (as a potential transition). The state machine will eventually check if the transition is accepted or rejected 
	/// by the target state (CheckEnterTransition). Call this method from within the CheckExitTransition method. 
	/// </summary>
	/// <example>
	/// For instance, if you need to transition to multiple states.
	/// <code>
	/// if( conditionA )
	/// {	
	/// 	EnqueueTransition<TargetStateA>();
	/// }
	/// else if( conditionB )
	/// {
	/// 	EnqueueTransition<TargetStateB>();
	/// 	EnqueueTransition<TargetStateC>(); 	
	/// }
	/// </code>	
	/// </example>
	public void EnqueueTransition<T>() where T : CharacterState
	{
		CharacterState state = GetState<T>();

		if( state != null )
			transitionsQueue.Enqueue( state );
	}

	
	void Awake()
    {		
		CharacterActor = this.GetComponentInBranch<CharacterActor>();		
		CharacterBrain = this.GetComponentInBranch< CharacterActor , CharacterBrain >();
			
		AddStates();
		
	}	

	bool CanCurrentStateOverrideAnimatorController => currentState.OverrideAnimatorController && Animator != null && currentState.RuntimeAnimatorController != null;

	void Start()
	{
		characterInitialForward = CharacterActor.Forward;

		if( !currentState.enabled )
			currentState = null;
		
		if( currentState != null )
		{
			
			currentState.EnterBehaviour( 0f , currentState );

			if( CanCurrentStateOverrideAnimatorController )
				Animator.runtimeAnimatorController = currentState.RuntimeAnimatorController;
				
			
		}
	}
	
	void OnEnable()
	{
		CharacterActor.OnPreSimulation += PreCharacterSimulation;

		if( Animator != null )
			CharacterActor.OnAnimatorIKEvent += OnAnimatorIK;
	}

	

	void OnDisable()
	{
		CharacterActor.OnPreSimulation -= PreCharacterSimulation;

		if( Animator != null )
			CharacterActor.OnAnimatorIKEvent -= OnAnimatorIK;
	}

	void AddStates()
	{
		
		CharacterState[] statesArray = CharacterActor.GetComponentsInChildren<CharacterState>();
		for (int i = 0; i < statesArray.Length ; i++)
		{
			CharacterState state = statesArray[i];
			string stateName = state.GetType().Name;

			// The state is already included, ignore it!
			if( GetState( stateName ) != null )
			{ 
				Debug.Log("Warning: GameObject " + state.gameObject.name + " has the state " + stateName + " repeated in the hierarchy.");
				continue;
			}
			
			states.Add( stateName ,  state );
		}		

	}

	/// <summary>
	/// Sets a flag that resets all the IK weights (hands and feet) during the next OnAnimatorIK call.
	/// </summary>
	public void ResetIKWeights()
	{
		CharacterActor.ResetIKWeights();				
	}
	
	void FixedUpdate()
	{
		float dt = Time.deltaTime;

		if( CharacterActor == null )
			return;

		if( currentState == null )
			return;

		if( !currentState.isActiveAndEnabled )
			return;
		
		
		bool changeOfState = CheckForTransitions();

		// Reset all transitions
		transitionsQueue.Clear();

		if( changeOfState )
		{
			previousState.ExitBehaviour( dt , currentState );
			
			if( CanCurrentStateOverrideAnimatorController )
				Animator.runtimeAnimatorController = currentState.RuntimeAnimatorController;	
			
			

			currentState.EnterBehaviour( dt , previousState );
			
		}		
		
		currentState.PreUpdateBehaviour( dt );
		currentState.UpdateBehaviour( dt );
		currentState.PostUpdateBehaviour( dt );
		
		
	}

	void PreCharacterSimulation( float dt )
	{
		
		if( Animator == null )
            return;

        if( Animator.runtimeAnimatorController == null )
            return;

        if( !Animator.gameObject.activeSelf )
            return;

		currentState.PreCharacterSimulation( dt );
	}

	

	void OnAnimatorIK( int layerIndex )
	{
		if( currentState == null )
			return;
		
		currentState.UpdateIK( layerIndex );
	}
	

	Queue<CharacterState> transitionsQueue = new Queue<CharacterState>();

	bool CheckForTransitions()
	{
		currentState.CheckExitTransition();

		CharacterState nextState = null;
		
		while( transitionsQueue.Count != 0 )
		{
			CharacterState thisState = transitionsQueue.Dequeue();
			if( thisState == null )
				continue;

			if( !thisState.enabled )
				continue;
			
			bool success = thisState.CheckEnterTransition( currentState );
			
			if( success )
			{
				nextState = thisState;
				
				if( OnStateChange != null )
					OnStateChange( currentState , nextState );

				previousState = currentState;
				currentState = nextState;

				return true;
			}
		}		

		return false;
		
	}

	
	/// <summary>
	/// Gets a vector that is the product of the input axes (taken from the character actions) and the movement reference. 
	/// The magnitude of this vector is always less than or equal to 1.
	/// </summary>
	public Vector3 InputMovementReference
	{
		get
		{
			if( CharacterBrain == null )
				return Vector3.zero;
			
			if( CharacterActor.Is2D )
			{
				return CharacterBrain.CharacterActions.movement.value.x * MovementReferenceRight;
			}
			else
			{
				Vector3 inputMovementReference = CharacterBrain.CharacterActions.movement.value.x * MovementReferenceRight +
					CharacterBrain.CharacterActions.movement.value.y * MovementReferenceForward;
				
				return Vector3.ClampMagnitude( inputMovementReference , 1f );
			}
		}
        
    }

	public Transform ExternalReference
	{
		get
		{
			return movementReferenceParameters.externalReference;
		}
		set
		{
			movementReferenceParameters.externalReference = value;
		}
        
    }

	public MovementReferenceMode MovementReferenceMode
	{
		get
		{
			return movementReferenceParameters.movementReferenceMode;
		}
		set
		{
			movementReferenceParameters.movementReferenceMode = value;
		}
        
    }

	Vector3 characterInitialForward = Vector3.forward;

	public Vector3 MovementReferenceForward
	{
		get
		{
			Vector3 output = Vector3.zero;
			
			switch( movementReferenceParameters.movementReferenceMode )
			{
				case MovementReferenceMode.World:

					output = Vector3.forward;
					break;

				case MovementReferenceMode.Character:
					
					output = characterInitialForward;
					break;

				case MovementReferenceMode.External:					


					if( movementReferenceParameters.externalReference != null )
						output = Vector3.ProjectOnPlane( movementReferenceParameters.externalReference.forward , CharacterActor.Up ).normalized;
					else
						Debug.Log( "CharacterStateController: the external reference is null! assign a Transform." );
					
					break;
			}

			return output;
			
		}
        
    }

	public Vector3 MovementReferenceRight
	{
		get
		{	
			return Vector3.Cross( CharacterActor.Up , MovementReferenceForward ).normalized;		
			// return Vector3.Cross( CharacterActor.GroundStableNormal , MovementReferenceForward ).normalized;
		}
        
    }


}

[System.Serializable]
public class MovementReferenceParameters
{    

	[Tooltip("Select what type of movement reference the player should be using. Should the character use its own transform, the world coordinates, or an external transform?")]
    public MovementReferenceMode movementReferenceMode = MovementReferenceMode.World;

	[Tooltip("The external transform used by the \"External\" movement reference.")]
	/// <summary>
	/// The reference transform used as a movement reference ( "External" mode ). 
	/// </summary>
    public Transform externalReference = null;
}

public enum MovementReferenceMode
{
	World ,
	External ,
	Character
}

}

using UnityEngine;
using Lightbug.CharacterControllerPro.Core;
using Lightbug.Utilities;
using Lightbug.CharacterControllerPro.Implementation;

namespace Lightbug.CharacterControllerPro.Demo
{

[AddComponentMenu("Character Controller Pro/Demo/Character/States/Normal Movement")]
public class NormalMovement : CharacterState
{

    [Space(10)]
    
    public PlanarMovementParameters planarMovementParameters = new PlanarMovementParameters();

    public VerticalMovementParameters verticalMovementParameters = new VerticalMovementParameters(); 

    public CrouchParameters crouchParameters = new CrouchParameters();
   
    public LookingDirectionParameters lookingDirectionParameters = new LookingDirectionParameters();
    
    
    [Header("Animation")]

    [SerializeField]
    protected string groundedParameter = "Grounded";

    [SerializeField]
    protected string stableParameter = "Stable";

    [SerializeField]
    protected string verticalSpeedParameter = "VerticalSpeed";

    [SerializeField]
    protected string planarSpeedParameter = "PlanarSpeed";

	[SerializeField]
    protected string horizontalAxisParameter = "HorizontalAxis";

	[SerializeField]
    protected string verticalAxisParameter = "VerticalAxis";

    [SerializeField]
    protected string heightParameter = "Height";

    
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    
    #region Events	

    /// <summary>
    /// Event triggered when the character jumps.
    /// </summary>
	public event System.Action OnJumpPerformed;

    /// <summary>
    /// Event triggered when the character jumps from the ground.
    /// </summary>
	public event System.Action<bool> OnGroundedJumpPerformed;

    /// <summary>
    /// Event triggered when the character jumps while.
    /// </summary>
	public event System.Action<int> OnNotGroundedJumpPerformed;
	
	#endregion

    
    protected MaterialController materialController = null;    
    protected int notGroundedJumpsLeft = 0;   
    protected bool isAllowedToCancelJump = false;    
    protected bool wantToRun = false;    
    protected float currentPlanarSpeedLimit = 0f;

    protected bool groundedJumpAvailable = true;
    protected Vector3 jumpDirection = default( Vector3 );

    protected Vector3 targetLookingDirection = default( Vector3 );
    protected float targetHeight = 1f;

    protected bool wantToCrouch = false;
    protected bool isCrouched = false;

    protected PlanarMovementParameters.PlanarMovementProperties currentMotion = new PlanarMovementParameters.PlanarMovementProperties();
    
    protected override void Awake()
    {
        base.Awake();
        
        notGroundedJumpsLeft = verticalMovementParameters.availableNotGroundedJumps;       

        materialController = this.GetComponentInBranch< CharacterActor , MaterialController>();
        if( materialController == null )
        {
            Debug.Log( "Missing MaterialController component" );
            this.enabled = false;
            return;
        }
         

    }

    protected virtual void OnValidate()
    {
        verticalMovementParameters.OnValidate();
    }

    protected override void Start()
    {
        base.Start();

        targetHeight = CharacterActor.DefaultBodySize.y;

        float minShrinkHeightRatio = CharacterActor.BodySize.x / CharacterActor.BodySize.y;
        crouchParameters.heightRatio = Mathf.Max( minShrinkHeightRatio , crouchParameters.heightRatio );         
        
    }

    protected virtual void OnEnable()
    {
        CharacterActor.OnTeleport += OnTeleport;
        
    }

    protected virtual void OnDisable()
    {
        CharacterActor.OnTeleport -= OnTeleport;
    }

    public override string GetInfo()
    {
        return "This state serves as a multi purpose movement based state. It is responsible for handling gravity and jump, walk and run, crouch, " + 
        "react to the different material properties, etc. Basically it covers all the common movements involved " + 
        "in a typical game, from a 3D platformer to a first person walking simulator.";
    }

    void OnTeleport( Vector3 position , Quaternion rotation )
    {
        targetLookingDirection = CharacterActor.Forward;
        isAllowedToCancelJump = false;
    }


    /// <summary>
    /// Gets/Sets the useGravity toggle. Use this property to enable/disable the effect of gravity on the character.
    /// </summary>
    /// <value></value>
    public bool UseGravity
    {
        get
        {
            return verticalMovementParameters.useGravity;
        }
        set
        {
            verticalMovementParameters.useGravity = value;
        }
    }

    

    public override void CheckExitTransition()
    {
        
        if( CharacterActions.jetPack.value )
        {
            CharacterStateController.EnqueueTransition<JetPack>();
        }
        else if( CharacterActions.dash.Started )
        {
            CharacterStateController.EnqueueTransition<Dash>();       
        }
        else if( CharacterActor.Triggers.Count != 0 )
        {         
            CharacterStateController.EnqueueTransition<LadderClimbing>();
            CharacterStateController.EnqueueTransition<RopeClimbing>();
        }
        else if( !CharacterActor.IsGrounded )
        {            
            if( !CharacterActions.crouch.value )          
                CharacterStateController.EnqueueTransition<WallSlide>();
            
            CharacterStateController.EnqueueTransition<LedgeHanging>();
            
            
        }
        
    }

    
    void SetMotionValues()
    {
       
        switch( CharacterActor.CurrentState )
        {
            case CharacterActorState.StableGrounded:
                currentMotion.acceleration = planarMovementParameters.stableGroundedAcceleration;
                currentMotion.deceleration = planarMovementParameters.stableGroundedDeceleration;
                break;
            case CharacterActorState.UnstableGrounded:              
                currentMotion.acceleration = planarMovementParameters.unstableGroundedAcceleration;
                currentMotion.deceleration = planarMovementParameters.unstableGroundedDeceleration;
                break;
            case CharacterActorState.NotGrounded:              
                currentMotion.acceleration = planarMovementParameters.notGroundedAcceleration;
                currentMotion.deceleration = planarMovementParameters.notGroundedDeceleration;
                break;
 
        }   
        


        // Material values
        if( CharacterActor.IsGrounded )
        {
            currentMotion.acceleration *= materialController.CurrentSurface.accelerationMultiplier * materialController.CurrentVolume.accelerationMultiplier;
            currentMotion.deceleration *= materialController.CurrentSurface.decelerationMultiplier * materialController.CurrentVolume.decelerationMultiplier;
        }
        else
        {            
            currentMotion.acceleration *= materialController.CurrentVolume.accelerationMultiplier;
            currentMotion.deceleration *= materialController.CurrentVolume.decelerationMultiplier;
        }
        

    }
    
    /// <summary>
    /// Processes the lateral movement of the character (stable and unstable state), that is, walk, run, crouch, etc. 
    /// This movement is tied directly to the "movement" character action.
    /// </summary>
    protected virtual void ProcessPlanarMovement( float dt )
    {
        SetMotionValues();

        float speedMultiplier = materialController.CurrentSurface.speedMultiplier * materialController.CurrentVolume.speedMultiplier;

        bool needToAccelerate = false;

        switch( CharacterActor.CurrentState )
        {
            case CharacterActorState.NotGrounded:
                
                if( CharacterActor.WasGrounded )
                {
                    currentPlanarSpeedLimit = Mathf.Max( CharacterActor.PlanarVelocity.magnitude , planarMovementParameters.baseSpeedLimit );
                }
                

                needToAccelerate = ( CharacterStateController.InputMovementReference * currentPlanarSpeedLimit ).magnitude >= CharacterActor.PlanarVelocity.magnitude;

                
                CharacterActor.PlanarVelocity = Vector3.MoveTowards( 
                    CharacterActor.PlanarVelocity , 
                    currentPlanarSpeedLimit * CharacterStateController.InputMovementReference * speedMultiplier , 
                    ( needToAccelerate ? currentMotion.acceleration : currentMotion.deceleration ) * dt 
                );

                break;
            case CharacterActorState.StableGrounded:

                
                
                // Run ------------------------------------------------------------
                if( planarMovementParameters.runInputMode == InputMode.Toggle )
                {
                    if( CharacterActions.run.Started )
                        wantToRun = !wantToRun;            
                } 
                else
                {
                    wantToRun = CharacterActions.run.value;                        
                }

                if( wantToCrouch || !planarMovementParameters.canRun )
                    wantToRun = false;


                if( isCrouched )
                {
                    currentPlanarSpeedLimit = planarMovementParameters.baseSpeedLimit * crouchParameters.speedMultiplier; 
                }  
                else
                {   
                    currentPlanarSpeedLimit = wantToRun ? planarMovementParameters.boostSpeedLimit : planarMovementParameters.baseSpeedLimit;  
                }   

                Vector3 targetPlanarVelocity = currentPlanarSpeedLimit * CharacterStateController.InputMovementReference * speedMultiplier;

                needToAccelerate = CharacterStateController.InputMovementReference != Vector3.zero;
                
                
                // Set the velocity
                CharacterActor.PlanarVelocity = Vector3.MoveTowards( 
                    CharacterActor.PlanarVelocity , 
                    targetPlanarVelocity , 
                    ( needToAccelerate ? currentMotion.acceleration : currentMotion.deceleration ) * dt 
                );
                
                
                break;
            case CharacterActorState.UnstableGrounded:                
                
                currentPlanarSpeedLimit = planarMovementParameters.baseSpeedLimit;                
                
                needToAccelerate = ( CharacterStateController.InputMovementReference * currentPlanarSpeedLimit ).magnitude >= CharacterActor.PlanarVelocity.magnitude;
                
                CharacterActor.PlanarVelocity = Vector3.MoveTowards( 
                    CharacterActor.PlanarVelocity , 
                    currentPlanarSpeedLimit * CharacterStateController.InputMovementReference * speedMultiplier , 
                    ( needToAccelerate ? currentMotion.acceleration : currentMotion.deceleration ) * dt 
                );

                break;
        }

    }

        
    
    protected virtual void ProcessGravity( float dt )
    {
        if( !verticalMovementParameters.useGravity )
            return;
        
        
        verticalMovementParameters.UpdateParameters();

        float gravityMultiplier = CharacterActor.LocalVelocity.y >= 0 ? 
        materialController.CurrentVolume.gravityAscendingMultiplier : 
        materialController.CurrentVolume.gravityDescendingMultiplier;

        float gravity = gravityMultiplier * verticalMovementParameters.gravity;

        
        if( !CharacterActor.IsStable )
            CharacterActor.VerticalVelocity += - CharacterActor.Up * ( gravity * dt );
        

    }
    
    
    protected bool UnstableGroundedJumpAvailable => !verticalMovementParameters.canJumpOnUnstableGround && CharacterActor.CurrentState == CharacterActorState.UnstableGrounded;

    

    public enum JumpResult
    {
        Invalid ,
        Grounded , 
        NotGrounded
    }

    JumpResult CanJump()
    {
        JumpResult jumpResult = JumpResult.Invalid;

        if( !verticalMovementParameters.canJump )
            return jumpResult;
        
        if( wantToCrouch )
            return jumpResult;
         

        switch( CharacterActor.CurrentState )
        {
            case CharacterActorState.StableGrounded:
                
                if( CharacterActions.jump.StartedElapsedTime <= verticalMovementParameters.preGroundedJumpTime && groundedJumpAvailable )
                {
                    jumpResult = JumpResult.Grounded;
                }
                
                break;
            case CharacterActorState.NotGrounded:

                if( CharacterActions.jump.Started )
                {

                    // First check if the "grounded jump" is available. If so, execute a "coyote jump".
                    if( CharacterActor.NotGroundedTime <= verticalMovementParameters.postGroundedJumpTime && groundedJumpAvailable )
                    {
                        jumpResult = JumpResult.Grounded;
                    }
                    else if( notGroundedJumpsLeft != 0 )  // Do a not grounded jump
                    {
                        jumpResult = JumpResult.NotGrounded;
                    }
                }
                
                break;
            case CharacterActorState.UnstableGrounded:
                
                if( CharacterActions.jump.StartedElapsedTime <= verticalMovementParameters.preGroundedJumpTime && verticalMovementParameters.canJumpOnUnstableGround )
                    jumpResult = JumpResult.Grounded;

                break;
        }

        return jumpResult;
    }

    

    protected virtual void ProcessJump( float dt )
    {
        ProcessRegularJump( dt );
        ProcessJumpDown( dt );
    }

    #region JumpDown

    protected virtual bool ProcessJumpDown( float dt )
    {
        if( !verticalMovementParameters.canJumpDown )
            return false;

        if( !CharacterActor.IsStable )
            return false;
        
        if( !CharacterActor.IsGroundAOneWayPlatform )
            return false;

        if( verticalMovementParameters.filterByTag )
        {
            if( !CharacterActor.GroundObject.CompareTag( verticalMovementParameters.jumpDownTag ) )
                return false;
        }

        if( !ProcessJumpDownAction() )
            return false;
        
        JumpDown( dt );
        
        return true;
    }


    protected virtual bool ProcessJumpDownAction()
    {
        return isCrouched && CharacterActions.jump.Started;
    }


    protected virtual void JumpDown( float dt )
    {

        float groundDisplacementExtraDistance = 0f;

        Vector3 groundDisplacement = CharacterActor.GroundVelocity * dt;
        // bool  CharacterActor.transform.InverseTransformVectorUnscaled( Vector3.Project( groundDisplacement , CharacterActor.Up ) ).y

        if( !CharacterActor.IsGroundAscending )
            groundDisplacementExtraDistance = ( CharacterActor.GroundVelocity * dt ).magnitude;

        CharacterActor.ForceNotGrounded();     
           
        CharacterActor.Position -= CharacterActor.Up * ( CharacterConstants.ColliderMinBottomOffset + verticalMovementParameters.jumpDownDistance + groundDisplacementExtraDistance );
        
        CharacterActor.VerticalVelocity -= CharacterActor.Up * verticalMovementParameters.jumpDownVerticalVelocity;
    }

    #endregion

    #region Jump

    protected virtual void ProcessRegularJump( float dt )
    {
        if( CharacterActor.IsGrounded )
        {
            notGroundedJumpsLeft = verticalMovementParameters.availableNotGroundedJumps;   
                        
            groundedJumpAvailable = true;
        }         
        

        if( isAllowedToCancelJump )
        {       
            if( verticalMovementParameters.cancelJumpOnRelease )
            {
                if( CharacterActions.jump.StartedElapsedTime >= verticalMovementParameters.cancelJumpMaxTime || CharacterActor.IsFalling )
                {
                    isAllowedToCancelJump = false;
                }
                else if( !CharacterActions.jump.value && CharacterActions.jump.StartedElapsedTime >= verticalMovementParameters.cancelJumpMinTime )
                {
                    // Get the velocity mapped onto the current jump direction
                    Vector3 projectedJumpVelocity = Vector3.Project( CharacterActor.Velocity , jumpDirection );

                    CharacterActor.Velocity -=  projectedJumpVelocity * ( 1f - verticalMovementParameters.cancelJumpMultiplier );

                    isAllowedToCancelJump = false;
                }
            }
        }
        else
        {      
            JumpResult jumpResult = CanJump();
                
            switch( jumpResult )
            {
                case JumpResult.Grounded:
                    groundedJumpAvailable = false;
                                            
                    break;
                case JumpResult.NotGrounded:
                    notGroundedJumpsLeft--;
                    
                    break;

                case JumpResult.Invalid:                        
                    return;
            }            
            
            // Events ---------------------------------------------------
            if( CharacterActor.IsGrounded )
            {       

                if( OnGroundedJumpPerformed != null )
                    OnGroundedJumpPerformed( true );
            }
            else
            {
                if( OnNotGroundedJumpPerformed != null )
                    OnNotGroundedJumpPerformed( notGroundedJumpsLeft );
            }

            if( OnJumpPerformed != null )
                OnJumpPerformed();

            // Define the jump direction ---------------------------------------------------
            jumpDirection = SetJumpDirection();

            // Force the not grounded state, without this the character will not leave the ground.     
            CharacterActor.ForceNotGrounded();
            
            // First remove any velocity associated with the jump direction.
            CharacterActor.Velocity -= Vector3.Project( CharacterActor.Velocity , jumpDirection );
            CharacterActor.Velocity += jumpDirection * verticalMovementParameters.jumpSpeed;
          
            
            if( verticalMovementParameters.cancelJumpOnRelease )
                isAllowedToCancelJump = true;
                
        }
                
		
    }

    /// <summary>
    /// Returns the jump direction vector whenever the jump action is started.
    /// </summary>
    protected virtual Vector3 SetJumpDirection()
    {
        return CharacterActor.Up;
    }

    #endregion    
    
    
    void ProcessVerticalMovement( float dt )
    {
        ProcessGravity( dt );        
        ProcessJump( dt );        
    }    



    public override void EnterBehaviour( float dt , CharacterState fromState )
    {
        
        CharacterActor.alwaysNotGrounded = false;

        targetLookingDirection = CharacterActor.Forward; 

        if( fromState == CharacterStateController.GetState<WallSlide>() )
        {            
            // "availableNotGroundedJumps + 1" because the update code will consume one jump!
            notGroundedJumpsLeft = verticalMovementParameters.availableNotGroundedJumps + 1;
        }
           
        currentPlanarSpeedLimit = Mathf.Max( CharacterActor.PlanarVelocity.magnitude , planarMovementParameters.baseSpeedLimit );
        
        CharacterActor.UseRootMotion = false;
    }      


    protected virtual void HandleRotation( float dt)
    {	        
        HandleLookingDirection( dt );
    }

    

    
    
    void HandleLookingDirection( float dt )
    {
        if( !lookingDirectionParameters.changeLookingDirection )
            return;

        switch( lookingDirectionParameters.lookingDirectionMode )
        {
            case LookingDirectionParameters.LookingDirectionMode.Movement:

                switch( CharacterActor.CurrentState )
                {
                    case CharacterActorState.NotGrounded:

                        SetTargetLookingDirection( lookingDirectionParameters.notGroundedLookingDirectionMode );
                        
                        break;
                    case CharacterActorState.StableGrounded:

                        SetTargetLookingDirection( lookingDirectionParameters.stableGroundedLookingDirectionMode );                        
                        
                        break;
                    case CharacterActorState.UnstableGrounded:

                        SetTargetLookingDirection( lookingDirectionParameters.unstableGroundedLookingDirectionMode );
                        
                        break;
                }

                break;

            case LookingDirectionParameters.LookingDirectionMode.ExternalReference:

                if( !CharacterActor.CharacterBody.Is2D )
                    targetLookingDirection = CharacterStateController.MovementReferenceForward;

                break;

            case LookingDirectionParameters.LookingDirectionMode.Target:

                targetLookingDirection = ( lookingDirectionParameters.target.position - CharacterActor.Position ).normalized;

                break;
        }

                

        Quaternion targetDeltaRotation = Quaternion.FromToRotation( CharacterActor.Forward , targetLookingDirection );
        Quaternion currentDeltaRotation = Quaternion.Slerp( Quaternion.identity , targetDeltaRotation , lookingDirectionParameters.speed * dt );

        
        if( CharacterActor.CharacterBody.Is2D )
        {            
            CharacterActor.Forward = targetLookingDirection;
        }
        else
        {      
            float angle = Vector3.Angle( CharacterActor.Forward , targetLookingDirection );
            
            if( CustomUtilities.isCloseTo( angle , 180f , 0.5f ) )
            {
                
                CharacterActor.Forward = Quaternion.Euler( 0f , 1f , 0f ) * CharacterActor.Forward;
            } 
                  
            CharacterActor.Forward = currentDeltaRotation * CharacterActor.Forward;
            
        }        
        
        
    }

    void SetTargetLookingDirection( LookingDirectionParameters.LookingDirectionMovementSource lookingDirectionMode )
    {
        if( lookingDirectionMode == LookingDirectionParameters.LookingDirectionMovementSource.Input )
        {
            if( CharacterStateController.InputMovementReference != Vector3.zero )
                targetLookingDirection = CharacterStateController.InputMovementReference;
            else
                targetLookingDirection = CharacterActor.Forward;
        }
        else
        {
            if( CharacterActor.PlanarVelocity != Vector3.zero )
                targetLookingDirection = CharacterActor.PlanarVelocity;
            else
                targetLookingDirection = CharacterActor.Forward;
        }
            
    }


    public override void UpdateBehaviour( float dt )
    {
        
        HandleSize( dt );
        HandleVelocity( dt );        
        HandleRotation( dt );

    }
    
    
    public override void PreCharacterSimulation( float dt )
    {          
        CharacterStateController.Animator.SetBool( groundedParameter , CharacterActor.IsGrounded );
        CharacterStateController.Animator.SetBool( stableParameter , CharacterActor.IsStable );
        CharacterStateController.Animator.SetFloat( verticalSpeedParameter , CharacterActor.LocalVelocity.y );
        CharacterStateController.Animator.SetFloat( planarSpeedParameter , CharacterActor.PlanarVelocity.magnitude );
        CharacterStateController.Animator.SetFloat( horizontalAxisParameter , CharacterActions.movement.value.x );
        CharacterStateController.Animator.SetFloat( verticalAxisParameter , CharacterActions.movement.value.y );	
        CharacterStateController.Animator.SetFloat( heightParameter , CharacterActor.BodySize.y );
    }
    
    
    protected virtual void HandleSize( float dt )
    {        
        // Want to crouch ---------------------------------------------------------------------    
        if( crouchParameters.enableCrouch )
        {
            if( crouchParameters.inputMode == InputMode.Toggle )
            {
                if( CharacterActions.crouch.Started )
                    wantToCrouch = !wantToCrouch;            
            } 
            else
            {
                wantToCrouch = CharacterActions.crouch.value;                
            }

            if( !crouchParameters.notGroundedCrouch && !CharacterActor.IsGrounded )
                wantToCrouch = false;
            

            if( CharacterActor.IsGrounded && wantToRun )
                wantToCrouch = false;

        }
        else
        {
            wantToCrouch = false;
        }


        
            

        // Process Size ----------------------------------------------------------------------------        
        targetHeight = wantToCrouch ? CharacterActor.DefaultBodySize.y * crouchParameters.heightRatio : CharacterActor.DefaultBodySize.y;

        Vector3 targetSize = new Vector2( CharacterActor.DefaultBodySize.x , targetHeight );
        
        bool validSize = CharacterActor.SetBodySize( targetSize );
    
        if( validSize )
            isCrouched = wantToCrouch;
        
    }

    

    protected virtual void HandleVelocity( float dt )
    {
        ProcessVerticalMovement( dt );
        ProcessPlanarMovement( dt );        
    }


}
    

}







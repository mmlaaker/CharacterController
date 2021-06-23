using UnityEngine;
using Lightbug.Utilities;

namespace Lightbug.CharacterControllerPro.Demo
{
    


/// <summary>
/// This abstract class represents a basic platform.
/// </summary>
public abstract class Platform : MonoBehaviour
{    

    /// <summary>
    /// Gets the RigidbodyComponent component associated to the character.
    /// </summary>
    public RigidbodyComponent RigidbodyComponent { get; protected set; }

    protected virtual void Awake()
    {		
        Rigidbody2D rigidbody2D = GetComponent<Rigidbody2D>();

        if( rigidbody2D != null )
        {
            RigidbodyComponent = gameObject.GetOrAddComponent<RigidbodyComponent2D>();
        }
        else
        {
            Rigidbody rigidbody3D = GetComponent<Rigidbody>();

            if( rigidbody3D != null )
            {
                RigidbodyComponent = gameObject.GetOrAddComponent<RigidbodyComponent3D>();
            }
        }
		
        
		if( RigidbodyComponent == null )
			this.enabled = false;
		
    }

}

}

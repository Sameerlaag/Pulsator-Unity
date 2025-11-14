using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ChassisController : MonoBehaviour
{

    private Animator animatorController;

    void Awake ()
    {
        animatorController = this.GetComponent<Animator>();
	}

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            SwitchChassis();
    }

    public void SwitchChassis()
    {
        if (animatorController != null)
            animatorController.SetTrigger("Chassis");
    }
}

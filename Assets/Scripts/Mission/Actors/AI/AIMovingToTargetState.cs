using UnityEngine;

/// <summary>
/// The actor is moving towards its target.
/// </summary>
public class AIMovingToTargetState : AIState
{
    protected override AIState _HandleInput(AIInput input)
    {
        //if(input.targetInOptimalRange && input.targetInLOS)
        //{
        //    return new AIHoldingPositionCombatState();
        //}

        return this;
    }

    public Vector3 followOffset;

    protected override void _StateUpdate()
    {
        //if ((_controller.transform.position - _controller.MoveTarget.position).magnitude > 5f)
        //{
            _controller.GetActor().Move(_controller.MoveTarget.position + followOffset);
        //}
    }
}

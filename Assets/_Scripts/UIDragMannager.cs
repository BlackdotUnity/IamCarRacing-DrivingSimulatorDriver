using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIDragMannager : MonoBehaviour, IDragHandler, IEndDragHandler
{
	[SerializeField]
	private bool isPressing = false;
	public void OnDrag(PointerEventData data)
	{

		//if (RCC_Settings.Instance.selectedControllerType != RCC_Settings.ControllerType.Mobile)
		//	return;

		isPressing = true;

		//RCC_SceneManager.Instance.activePlayerCamera.OnDrag(data);

	}

	public void OnEndDrag(PointerEventData data)
	{

		//if (RCC_Settings.Instance.selectedControllerType != RCC_Settings.ControllerType.Mobile)
		//	return;

		//isPressing = false;

	}

}

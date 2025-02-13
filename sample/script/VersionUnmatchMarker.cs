
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class VersionUnmatchMarker : UdonSharpBehaviour
{
	private GameObject gameObject;
    void Start()
    {
		gameObject = this.transform.GetChild(0).gameObject;
        gameObject.SetActive(false);
    }
	public void Show()
	{
        gameObject.SetActive(true);
	}
}

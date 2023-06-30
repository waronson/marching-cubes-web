using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class VoxelNetworkSocket : MonoBehaviour
{
	public SocketIOUnity sioCom;
	public bool IsConnected { get { return sioCom.Connected; } }
	public UnityEvent<VoxelEdit> VoxelEditEvent;

	[SerializeField] private string serverUri = "http://localhost";
	[SerializeField] Text connectionText;


	private void Start()
	{
		sioCom = new SocketIOUnity(serverUri);
		
		sioCom.On("connect", (response) => {
			sioCom.On("VoxelServerEdit", (edit) =>
			{
				VoxelEdit e = edit.GetValue<VoxelEdit>();
				VoxelEditEvent.Invoke(e);
			});

			connectionText.text = "Connected to: " + sioCom.ServerUri;
			Debug.Log("Connected to: " + sioCom.ServerUri);
		});

		sioCom.Connect();
	}

	private void OnDestroy()
	{
		sioCom.Disconnect();
	}

	public void SendEdit(VoxelEdit edit)
	{
		sioCom.Emit("VoxelClientEdit", edit);
	}
}

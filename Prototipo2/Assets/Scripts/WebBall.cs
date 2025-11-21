using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WebBall : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.CompareTag("Player"))
        {
            Debug.Log("Collisioned");
            Destroy(collision.transform.gameObject);
            //EditorApplication.ExitPlaymode();
            //SceneManager.LoadScene("Level1");
        }
    }
}

using UnityEngine;
using System.Collections;

public class ScannerDetector : MonoBehaviour
{
    public Transform scannerLight; // Ссылка на куб-индикатор
    public Material greenMat;      // Зеленый материал
    public Material redMat;        // Красный материал

    private bool isScanning = false; // Флаг, чтобы не запускать несколько раз

    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, что вошел объект с тегом Contraband
        if (other.CompareTag("Contraband") && !isScanning)
        {
            StartCoroutine(ScannerAlarm());
        }
    }

    IEnumerator ScannerAlarm()
    {
        isScanning = true;

        Debug.Log("ТРЕВОГА! Обнаружена контрабанда!");

        // Красим в красный
        scannerLight.GetComponent<Renderer>().material = redMat;

        // Ждем 5 секунд
        yield return new WaitForSeconds(5f);

        // Возвращаем в зеленый
        scannerLight.GetComponent<Renderer>().material = greenMat;
        Debug.Log("Сканер сброшен.");

        isScanning = false;
    }
}
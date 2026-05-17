"""
Detector simples de palmas para ativar o Nexus.

Ajuste CLAP_THRESHOLD se estiver sensível demais ou não detectar.
"""

import time
import requests
import numpy as np
import sounddevice as sd

BACKEND_URL = "http://localhost:5000"
CLAP_THRESHOLD = 0.35
COOLDOWN_SECONDS = 2.0
SAMPLE_RATE = 44100
BLOCK_SIZE = 1024

last_clap_time = 0


def activate_nexus():
    try:
        requests.post(f"{BACKEND_URL}/api/nexus/activate", timeout=2)
        print("Palma detectada → Nexus ativado.")
    except Exception as exc:
        print(f"Não consegui chamar o backend: {exc}")


def audio_callback(indata, frames, time_info, status):
    global last_clap_time

    if status:
        print(status)

    volume = float(np.linalg.norm(indata) / len(indata))
    now = time.time()

    if volume > CLAP_THRESHOLD and now - last_clap_time > COOLDOWN_SECONDS:
        last_clap_time = now
        activate_nexus()


def main():
    print("Detector de palmas iniciado.")
    print("Bata palma perto do microfone para ativar o Nexus.")
    print("Ctrl+C para sair.")

    with sd.InputStream(callback=audio_callback, channels=1, samplerate=SAMPLE_RATE, blocksize=BLOCK_SIZE):
        while True:
            time.sleep(0.1)


if __name__ == "__main__":
    main()

import subprocess
import platform


def open_app_windows(app_name: str):
    subprocess.Popen(app_name, shell=True)


def say_system_info():
    return {
        "os": platform.system(),
        "release": platform.release(),
        "machine": platform.machine(),
    }


if __name__ == "__main__":
    print(say_system_info())

import re


def mesh_name(key):
    return re.sub(r'(?<!^)(?=[A-Z])', '_', key).lower()

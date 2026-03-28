import random

from scipy.stats import beta as beta_dist

from utils.faker_instance import fake

def generate_user_characteristics_vector() -> dict:
    vector = {}

    vector["flavor_sweetness"] = {
        "value": round(float(beta_dist.rvs(2.5, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["flavor_bitterness"] = {
        "value": round(float(beta_dist.rvs(1.5, 3.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["flavor_spiciness"] = {
        "value": round(float(beta_dist.rvs(2.0, 2.5)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["flavor_umami"] = {
        "value": round(float(beta_dist.rvs(3.0, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["flavor_sourness"] = {
        "value": round(float(beta_dist.rvs(2.0, 2.5)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["flavor_saltiness"] = {
        "value": round(float(beta_dist.rvs(2.5, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }

    vector["texture_crispy"] = {
        "value": round(float(beta_dist.rvs(3.0, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["texture_creamy"] = {
        "value": round(float(beta_dist.rvs(2.5, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["texture_chewy"] = {
        "value": round(float(beta_dist.rvs(2.0, 2.5)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }

    vector["physics_richness"] = {
        "value": round(float(beta_dist.rvs(2.0, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["physics_temperature"] = {
        "value": round(float(beta_dist.rvs(2.5, 2.5)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["physics_freshness"] = {
        "value": round(float(beta_dist.rvs(3.5, 1.5)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }

    vector["context_price_sensitivity"] = {
        "value": round(float(beta_dist.rvs(2.0, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }
    vector["context_portion_preference"] = {
        "value": round(float(beta_dist.rvs(2.5, 2.0)), 3),
        "tolerance": round(random.uniform(0.1, 0.7), 3),
    }

    return vector

def generate_full_name() -> str:
    return fake.name()

def generate_phone() -> str:
    return f"+48 {random.randint(500, 999)} {random.randint(100, 999)} {random.randint(100, 999)}"

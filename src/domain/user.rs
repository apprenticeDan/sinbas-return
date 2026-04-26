use super::{user_error::UserError, email::Email};

#[derive(Clone)]
pub struct User {
    pub id: u64,
    pub email: Email,
    password_hash: String,
}

impl User {
    pub fn create(id: u64, email: String, password_hash: String) -> Result<User, UserError> {
        let emilio = Email::parse(email)
            .map_err(UserError::Email)?;

        Ok(Self { id, email: emilio, password_hash, })
    } 

    pub fn password_hash(&self) -> &str {
        &self.password_hash
    }
}

impl std::fmt::Debug for User {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("User")
            .field("id", &self.id)
            .field("email", &self.email.value())
            .field("password_hash", &"***")
            .finish()
    }
}

